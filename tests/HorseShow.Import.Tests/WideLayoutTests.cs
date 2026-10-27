using HorseShow.Core.Model;
using HorseShow.Import.Mapping;
using HorseShow.Import.Model;
using HorseShow.Import.Resolution;
using Microsoft.EntityFrameworkCore;

namespace HorseShow.Import.Tests;

public class WideLayoutTests
{
    [Fact]
    public void Wide_sheet_maps_class_columns()
    {
        using var s = Fixture.Open("wide-layout.csv");
        var doc = ImportPipeline.ParseCsv(s);
        var m = AutoMapper.Propose(doc);

        Assert.Equal(ImportField.BackNumber, m.FieldFor(0));
        Assert.Equal(ImportField.Rider, m.FieldFor(1));
        Assert.Equal(ImportField.Horse, m.FieldFor(2));
        Assert.Equal([3, 4, 5], m.ClassColumns().ToArray());
        Assert.True(m.IsWide);
        Assert.Empty(m.Validate(hasClassHeaders: false));
    }

    [Fact]
    public void Setting_ClassPlacing_on_a_second_column_keeps_the_first()
    {
        var m = new ColumnMapping();
        m.Set(3, ImportField.ClassPlacing);
        m.Set(4, ImportField.ClassPlacing);
        Assert.Equal(2, m.ClassColumns().Count());
    }

    [Fact]
    public async Task Wide_sheet_plans_one_row_per_filled_cell_and_imports()
    {
        await using var tmp = new TempDatabase();
        await tmp.Initializer.InitializeAsync();
        await using var db = await tmp.Factory.CreateDbContextAsync();
        var season = new Season { Name = "2026", Year = 2026, IsActive = true, DefaultPointSchedule = await db.PointSchedules.FirstAsync(p => p.Name.StartsWith("Classic")) };
        db.Seasons.Add(season);
        await db.SaveChangesAsync();

        using var s = Fixture.Open("wide-layout.csv");
        var doc = ImportPipeline.ParseCsv(s);
        var plan = await new ImportPlanner(new NameResolver()).BuildAsync(db, doc, AutoMapper.Propose(doc), season.Id, null);

        Assert.Equal(3, plan.Classes.Count);
        Assert.Equal(("12", null), (plan.Classes[0].ClassNumber, plan.Classes[0].Name));
        Assert.Equal(("15", "Walk-Trot Pleasure"), (plan.Classes[2].ClassNumber, plan.Classes[2].Name));
        Assert.Equal(3, plan.Classes[0].Rows.Count);
        Assert.Equal(3, plan.Classes[1].Rows.Count);
        Assert.Single(plan.Classes[2].Rows);
        Assert.Equal(7, plan.RowCount);
        Assert.Equal(0, plan.ErrorCount);
        Assert.Equal(PlacingStatus.Scratched, plan.Classes[1].Rows.Single(r => r.RiderText == "Ann Lee").Status);
        Assert.Equal("204", plan.Classes[0].Rows[0].BackNumber);

        var summary = await ImportCommitter.CommitAsync(db, plan, ImportSource.Csv, "wide-layout.csv", null, null);
        Assert.Equal(7, summary.ResultsAdded);
        Assert.Equal(3, summary.ClassesCreated);
        var hus = await db.ShowClasses.Include(c => c.Results).ThenInclude(r => r.Rider).FirstAsync(c => c.ClassNumber == "12");
        Assert.Equal("Class 12", hus.Name);
        Assert.Equal(10, hus.Results.Single(r => r.Rider!.Name == "Smith, Jane").Points);
    }
}
