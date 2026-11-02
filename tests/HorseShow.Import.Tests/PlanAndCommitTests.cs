using HorseShow.Core.Model;
using HorseShow.Data;
using HorseShow.Import;
using HorseShow.Import.Mapping;
using HorseShow.Import.Model;
using HorseShow.Import.Resolution;
using Microsoft.EntityFrameworkCore;

namespace HorseShow.Import.Tests;

public class PlanAndCommitTests
{
    private static readonly ImportPlanner Planner = new(new NameResolver());

    private static async Task<(TempDatabase tmp, int seasonId)> SeedSeasonAsync(bool withSchedule = true)
    {
        var tmp = new TempDatabase();
        await tmp.Initializer.InitializeAsync();
        await using var db = await tmp.Factory.CreateDbContextAsync();
        var schedule = withSchedule ? await db.PointSchedules.FirstAsync(p => p.Name.StartsWith("Classic")) : null;
        var season = new Season { Name = "2026", Year = 2026, IsActive = true, DefaultPointSchedule = schedule };
        db.Seasons.Add(season);
        await db.SaveChangesAsync();
        return (tmp, season.Id);
    }

    [Fact]
    public async Task Multi_class_paste_plans_three_classes_with_normalized_rows()
    {
        var (tmp, seasonId) = await SeedSeasonAsync();
        await using var _ = tmp;
        var doc = ImportPipeline.ParseText(Fixture.Text("multi-class-sections.txt"));
        var mapping = AutoMapper.Propose(doc);

        await using var db = await tmp.Factory.CreateDbContextAsync();
        var plan = await Planner.BuildAsync(db, doc, mapping, seasonId, showId: null);

        Assert.Equal(3, plan.Classes.Count);
        Assert.Equal(("12", "Hunter Under Saddle", 8), (plan.Classes[0].ClassNumber, plan.Classes[0].Name, plan.Classes[0].EntryCount));
        Assert.Equal(10, plan.RowCount);
        Assert.Equal(0, plan.ErrorCount);

        var scratched = plan.Classes[1].Rows.Single(r => r.RiderText == "Ann Lee");
        Assert.Equal(PlacingStatus.Scratched, scratched.Status);
        Assert.Null(scratched.Placing);

        var ordinal = plan.Classes[2].Rows.First();
        Assert.Equal(1, ordinal.Placing);
        Assert.Equal("52", ordinal.BackNumber);

        Assert.Equal(7, plan.Riders.Count); // Jane, Bob, Ann, Sean, Lily, Noah, Mia
        Assert.All(plan.Riders, r => Assert.Equal(ResolutionStatus.New, r.Status));
        Assert.Equal(2, plan.Riders.Single(r => r.Text == "Jane Smith").Occurrences);
    }

    [Fact]
    public async Task Plan_flags_duplicate_placings_and_placings_above_entry_count()
    {
        var (tmp, seasonId) = await SeedSeasonAsync();
        await using var _ = tmp;
        var doc = ImportPipeline.ParseText("Class 1 - Test (2 entries)\nPlace\tRider\n1\tA\n1\tB\n3\tC\n");
        await using var db = await tmp.Factory.CreateDbContextAsync();

        var plan = await Planner.BuildAsync(db, doc, AutoMapper.Propose(doc), seasonId, null);

        var rows = plan.Classes.Single().Rows;
        Assert.Contains(rows[0].Issues, i => i.Message.Contains("appears 2 times"));
        Assert.Contains(rows[2].Issues, i => i.Message.Contains("above the 2 entries"));
        Assert.Equal(0, plan.ErrorCount);
        Assert.Equal(3, plan.WarningCount);
    }

    [Fact]
    public async Task Rows_without_names_or_with_unreadable_placings_are_errors_and_excluded()
    {
        var (tmp, seasonId) = await SeedSeasonAsync();
        await using var _ = tmp;
        var doc = ImportPipeline.ParseText("Place\tBack #\tRider\tHorse\n1\t204\tJane Smith\tBlue Moon\nabc\t117\tBob Jones\tRed Sky\n3\t88\t\t\n");
        await using var db = await tmp.Factory.CreateDbContextAsync();

        var plan = await Planner.BuildAsync(db, doc, AutoMapper.Propose(doc), seasonId, null);

        var rows = plan.Classes.Single().Rows;
        Assert.True(rows[0].WillImport);
        Assert.False(rows[1].WillImport);
        Assert.False(rows[2].WillImport);
        Assert.Equal(2, plan.ErrorCount);
        Assert.Equal(1, plan.ImportableRowCount);
    }

    [Fact]
    public async Task Commit_creates_show_classes_participants_results_and_computes_points()
    {
        var (tmp, seasonId) = await SeedSeasonAsync();
        await using var _ = tmp;
        var text = Fixture.Text("multi-class-sections.txt");
        var doc = ImportPipeline.ParseText(text);
        await using var db = await tmp.Factory.CreateDbContextAsync();
        var plan = await Planner.BuildAsync(db, doc, AutoMapper.Propose(doc), seasonId, null);
        plan.NewShowName = "Spring Classic";
        plan.NewShowDate = new DateOnly(2026, 4, 12);

        var summary = await ImportCommitter.CommitAsync(db, plan, ImportSource.Paste, null, ImportCommitter.Hash(text), null);

        Assert.Equal(10, summary.ResultsAdded);
        Assert.Equal(3, summary.ClassesCreated);
        Assert.Equal(7, summary.RidersCreated);
        Assert.Equal(7, summary.HorsesCreated);

        await using var check = await tmp.Factory.CreateDbContextAsync();
        var show = await check.Shows.Include(s => s.Classes).ThenInclude(c => c.Results).ThenInclude(r => r.Rider).SingleAsync();
        Assert.Equal("Spring Classic", show.Name);
        Assert.Equal(summary.BatchId, show.CreatedByImportBatchId);
        var hus = show.Classes.Single(c => c.ClassNumber == "12");
        Assert.Equal(8, hus.EntryCount);
        Assert.Equal(10, hus.Results.Single(r => r.Rider!.Name == "Jane Smith").Points);
        Assert.Equal(6, hus.Results.Single(r => r.Rider!.Name == "Bob Jones").Points);
        Assert.All(hus.Results, r => Assert.Equal(summary.BatchId, r.ImportBatchId));
        Assert.Equal(10, (await check.ImportBatches.SingleAsync()).RowCount);
    }

    [Fact]
    public async Task Imported_points_column_is_kept_as_published_not_recomputed()
    {
        var (tmp, seasonId) = await SeedSeasonAsync();
        await using var _ = tmp;
        using var s = Fixture.Open("with-header-classes.csv");
        var doc = ImportPipeline.ParseCsv(s);
        await using var db = await tmp.Factory.CreateDbContextAsync();
        var plan = await Planner.BuildAsync(db, doc, AutoMapper.Propose(doc), seasonId, null);

        await ImportCommitter.CommitAsync(db, plan, ImportSource.Csv, "with-header-classes.csv", null, null);

        var results = await db.Results.Include(r => r.Rider).Include(r => r.ShowClass).ToListAsync();
        var janeHus = results.Single(r => r.Rider!.Name == "Smith, Jane" && r.ShowClass!.ClassNumber == "12");
        Assert.Equal(10, janeHus.Points);
        Assert.Equal(PointsSource.Imported, janeHus.PointsSource);
        Assert.Equal(84.5m, janeHus.Score);

        var dq = results.Single(r => r.PlacingStatus == PlacingStatus.Disqualified);
        Assert.Null(dq.Points);
        Assert.Equal(PointsSource.Computed, dq.PointsSource); // no published points: engine owns it (and scores null)
    }

    [Fact]
    public async Task Second_import_into_the_same_show_matches_classes_and_honours_duplicate_policy()
    {
        var (tmp, seasonId) = await SeedSeasonAsync();
        await using var _ = tmp;
        var text = "Class 12 - Hunter Under Saddle\nPlace\tRider\tHorse\n1\tJane Smith\tBlue Moon\n2\tBob Jones\tRed Sky\n";
        await using var db = await tmp.Factory.CreateDbContextAsync();

        var doc1 = ImportPipeline.ParseText(text);
        var plan1 = await Planner.BuildAsync(db, doc1, AutoMapper.Propose(doc1), seasonId, null);
        var first = await ImportCommitter.CommitAsync(db, plan1, ImportSource.Paste, null, null, null);

        // Same class again with one corrected placing and one new rider.
        var text2 = "Class 12 - Hunter Under Saddle\nPlace\tRider\tHorse\n1\tBob Jones\tRed Sky\n2\tJane Smith\tBlue Moon\n3\tAnn Lee\tGrey Ghost\n";
        var doc2 = ImportPipeline.ParseText(text2);
        var plan2 = await Planner.BuildAsync(db, doc2, AutoMapper.Propose(doc2), seasonId, first.ShowId);

        var cls = Assert.Single(plan2.Classes);
        Assert.NotNull(cls.ExistingClassId);
        Assert.Equal(2, cls.ExistingResultCount);
        Assert.All(plan2.Riders.Where(r => r.Text != "Ann Lee"), r => Assert.Equal(ResolutionStatus.Exact, r.Status));

        plan2.DuplicatePolicy = DuplicatePolicy.Skip;
        var skip = await ImportCommitter.CommitAsync(db, plan2, ImportSource.Paste, null, null, null);
        Assert.Equal((1, 0, 2), (skip.ResultsAdded, skip.ResultsReplaced, skip.ResultsSkipped));
        Assert.Equal(3, await db.Results.CountAsync());
        Assert.Equal(1, (await db.Results.Include(r => r.Rider).SingleAsync(r => r.Rider!.Name == "Jane Smith")).Placing);

        var plan3 = await Planner.BuildAsync(db, doc2, AutoMapper.Propose(doc2), seasonId, first.ShowId);
        plan3.DuplicatePolicy = DuplicatePolicy.Replace;
        var replace = await ImportCommitter.CommitAsync(db, plan3, ImportSource.Paste, null, null, null);
        Assert.Equal((0, 3, 0), (replace.ResultsAdded, replace.ResultsReplaced, replace.ResultsSkipped));
        Assert.Equal(3, await db.Results.CountAsync());
        Assert.Equal(2, (await db.Results.Include(r => r.Rider).SingleAsync(r => r.Rider!.Name == "Jane Smith")).Placing);
        Assert.Equal(0, await db.ShowClasses.CountAsync(c => c.ClassNumber == "12") - 1);
    }

    [Fact]
    public async Task Suggested_match_adds_an_alias_so_the_next_import_is_exact()
    {
        var (tmp, seasonId) = await SeedSeasonAsync();
        await using var _ = tmp;
        await using var db = await tmp.Factory.CreateDbContextAsync();
        db.Riders.Add(new Rider { Name = "Jane Smith", Aliases = { new RiderAlias { Alias = "jane smith" } } });
        await db.SaveChangesAsync();

        var doc = ImportPipeline.ParseText("Place\tRider\n1\tJane Smyth\n");
        var plan = await Planner.BuildAsync(db, doc, AutoMapper.Propose(doc), seasonId, null);
        var res = Assert.Single(plan.Riders);
        Assert.Equal(ResolutionStatus.Suggested, res.Status);
        Assert.Equal("Jane Smith", res.ChosenName);

        await ImportCommitter.CommitAsync(db, plan, ImportSource.Paste, null, null, null);

        Assert.Equal(1, await db.Riders.CountAsync());
        Assert.Contains(await db.RiderAliases.ToListAsync(), a => a.Alias == "jane smyth");
        var again = await Planner.BuildAsync(db, doc, AutoMapper.Propose(doc), seasonId, null);
        Assert.Equal(ResolutionStatus.Exact, Assert.Single(again.Riders).Status);
    }

    [Fact]
    public async Task Rejecting_a_suggestion_creates_a_new_rider()
    {
        var (tmp, seasonId) = await SeedSeasonAsync();
        await using var _ = tmp;
        await using var db = await tmp.Factory.CreateDbContextAsync();
        db.Riders.Add(new Rider { Name = "Jane Smith", Aliases = { new RiderAlias { Alias = "jane smith" } } });
        await db.SaveChangesAsync();

        var doc = ImportPipeline.ParseText("Place\tRider\n1\tJane Smyth\n");
        var plan = await Planner.BuildAsync(db, doc, AutoMapper.Propose(doc), seasonId, null);
        plan.Riders[0].ChosenId = null;
        plan.Riders[0].ChosenName = null;

        await ImportCommitter.CommitAsync(db, plan, ImportSource.Paste, null, null, null);

        Assert.Equal(2, await db.Riders.CountAsync());
    }

    [Fact]
    public async Task Undo_removes_results_and_the_classes_and_show_the_import_created()
    {
        var (tmp, seasonId) = await SeedSeasonAsync();
        await using var _ = tmp;
        var text = Fixture.Text("multi-class-sections.txt");
        await using var db = await tmp.Factory.CreateDbContextAsync();
        var doc = ImportPipeline.ParseText(text);
        var plan = await Planner.BuildAsync(db, doc, AutoMapper.Propose(doc), seasonId, null);
        var summary = await ImportCommitter.CommitAsync(db, plan, ImportSource.Paste, null, null, null);

        var (results, classes, showDeleted) = await ImportCommitter.UndoAsync(db, summary.BatchId);

        Assert.Equal((10, 3, true), (results, classes, showDeleted));
        Assert.Equal(0, await db.Results.CountAsync());
        Assert.Equal(0, await db.Shows.CountAsync());
        Assert.Equal(0, await db.ImportBatches.CountAsync());
        Assert.Equal(7, await db.Riders.CountAsync()); // kept on purpose
    }

    [Fact]
    public async Task Undo_into_an_existing_show_keeps_the_show_and_manually_added_results()
    {
        var (tmp, seasonId) = await SeedSeasonAsync();
        await using var _ = tmp;
        await using var db = await tmp.Factory.CreateDbContextAsync();
        var show = new Show { SeasonId = seasonId, Name = "Manual", StartDate = new DateOnly(2026, 5, 1) };
        var cls = new ShowClass { Name = "Hunter Under Saddle", ClassNumber = "12", Show = show };
        cls.Results.Add(new Result { Placing = 1, Rider = new Rider { Name = "Manual Rider", Aliases = { new RiderAlias { Alias = "manual rider" } } } });
        db.Add(cls);
        await db.SaveChangesAsync();

        var doc = ImportPipeline.ParseText("Class 12 - Hunter Under Saddle\nPlace\tRider\n2\tJane Smith\n\nClass 13 - New Class\nPlace\tRider\n1\tJane Smith\n");
        var plan = await Planner.BuildAsync(db, doc, AutoMapper.Propose(doc), seasonId, show.Id);
        var summary = await ImportCommitter.CommitAsync(db, plan, ImportSource.Paste, null, null, null);
        Assert.Equal(1, summary.ClassesCreated);

        var (results, classes, showDeleted) = await ImportCommitter.UndoAsync(db, summary.BatchId);

        Assert.Equal((2, 1, false), (results, classes, showDeleted));
        Assert.Equal(1, await db.Results.CountAsync());
        Assert.Equal(1, await db.ShowClasses.CountAsync());
        Assert.Equal(1, await db.Shows.CountAsync());
    }

    [Fact]
    public async Task Combined_column_and_space_separated_fixtures_import_cleanly()
    {
        var (tmp, seasonId) = await SeedSeasonAsync();
        await using var _ = tmp;
        await using var db = await tmp.Factory.CreateDbContextAsync();

        foreach (var fixture in new[] { "combined-rider-horse.txt", "space-separated-pdf.txt", "wide-score-sheet.csv" })
        {
            var doc = ImportPipeline.ParseText(Fixture.Text(fixture));
            var mapping = AutoMapper.Propose(doc);
            if (fixture == "space-separated-pdf.txt")
            {
                // The auto-mapper guessed rider/horse in column order; a user would flip them. Do that here.
                mapping.Set(2, ImportField.Horse);
                mapping.Set(3, ImportField.Rider);
            }

            var plan = await Planner.BuildAsync(db, doc, mapping, seasonId, null);
            Assert.True(plan.ErrorCount == 0, $"{fixture}: {string.Join("; ", plan.Classes.SelectMany(c => c.Rows).SelectMany(r => r.Issues).Select(i => i.Message))}");
            var summary = await ImportCommitter.CommitAsync(db, plan, ImportSource.Paste, fixture, null, null);
            Assert.True(summary.ResultsAdded > 0, fixture);
        }

        var combined = await db.Results.Include(r => r.Rider).Include(r => r.Horse).Include(r => r.ShowClass)
            .Where(r => r.ShowClass!.ClassNumber == "3").ToListAsync();
        Assert.Equal(4, combined.Count);
        var names = string.Join(" | ", combined.Select(r => $"{r.Placing}/{r.PlacingStatus}: {r.Rider?.Name} on {r.Horse?.Name}"));
        Assert.True(combined.Any(r => r.Rider?.Name == "Ann Lee"), names);
        Assert.Equal("Grey Ghost", combined.Single(r => r.Rider!.Name == "Ann Lee").Horse!.Name);
        Assert.Equal(PlacingStatus.Disqualified, combined.Single(r => r.Rider!.Name == "Lily Chen").PlacingStatus);

        var pct = await db.Results.Include(r => r.Rider).Where(r => r.Score != null && r.Placing == null).ToListAsync();
        Assert.Equal(3, pct.Count);
        Assert.Equal(68.750m, pct.Single(r => r.Rider!.Name == "Jane Smith").Score); // "Smith, Jane" resolved to the existing rider
    }
}
