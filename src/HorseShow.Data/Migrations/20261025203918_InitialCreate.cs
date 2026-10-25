using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HorseShow.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Horses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Owner = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    RegistrationNumber = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    IsArchived = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Horses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MappingProfiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    SourceHint = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    ColumnMapJson = table.Column<string>(type: "TEXT", nullable: false),
                    ParserOptionsJson = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MappingProfiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Riders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Barn = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    IsArchived = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Riders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HorseAliases",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    HorseId = table.Column<int>(type: "INTEGER", nullable: false),
                    Alias = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HorseAliases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HorseAliases_Horses_HorseId",
                        column: x => x.HorseId,
                        principalTable: "Horses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RiderAliases",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RiderId = table.Column<int>(type: "INTEGER", nullable: false),
                    Alias = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RiderAliases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RiderAliases_Riders_RiderId",
                        column: x => x.RiderId,
                        principalTable: "Riders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Divisions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SeasonId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    PointScheduleId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Divisions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ImportBatches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SeasonId = table.Column<int>(type: "INTEGER", nullable: false),
                    ShowId = table.Column<int>(type: "INTEGER", nullable: true),
                    Source = table.Column<int>(type: "INTEGER", nullable: false),
                    FileName = table.Column<string>(type: "TEXT", maxLength: 260, nullable: true),
                    ImportedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    RowCount = table.Column<int>(type: "INTEGER", nullable: false),
                    RawContentHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    MappingProfileId = table.Column<int>(type: "INTEGER", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportBatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ImportBatches_MappingProfiles_MappingProfileId",
                        column: x => x.MappingProfileId,
                        principalTable: "MappingProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "PointScheduleRows",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PointScheduleId = table.Column<int>(type: "INTEGER", nullable: false),
                    Placing = table.Column<int>(type: "INTEGER", nullable: false),
                    Points = table.Column<decimal>(type: "TEXT", precision: 9, scale: 3, nullable: false),
                    MinEntries = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PointScheduleRows", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PointSchedules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SeasonId = table.Column<int>(type: "INTEGER", nullable: true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Mode = table.Column<int>(type: "INTEGER", nullable: false),
                    IsBuiltIn = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PointSchedules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Seasons",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Year = table.Column<int>(type: "INTEGER", nullable: false),
                    Organization = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    StandingsBasis = table.Column<int>(type: "INTEGER", nullable: false),
                    TopNShowsCounted = table.Column<int>(type: "INTEGER", nullable: true),
                    MinShowsForAward = table.Column<int>(type: "INTEGER", nullable: true),
                    DefaultPointScheduleId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Seasons", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Seasons_PointSchedules_DefaultPointScheduleId",
                        column: x => x.DefaultPointScheduleId,
                        principalTable: "PointSchedules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Shows",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SeasonId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    StartDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    Venue = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Shows", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Shows_Seasons_SeasonId",
                        column: x => x.SeasonId,
                        principalTable: "Seasons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ShowClasses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ShowId = table.Column<int>(type: "INTEGER", nullable: false),
                    DivisionId = table.Column<int>(type: "INTEGER", nullable: true),
                    ClassNumber = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ClassType = table.Column<int>(type: "INTEGER", nullable: false),
                    EntryCount = table.Column<int>(type: "INTEGER", nullable: true),
                    JudgeName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShowClasses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShowClasses_Divisions_DivisionId",
                        column: x => x.DivisionId,
                        principalTable: "Divisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ShowClasses_Shows_ShowId",
                        column: x => x.ShowId,
                        principalTable: "Shows",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Results",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ShowClassId = table.Column<int>(type: "INTEGER", nullable: false),
                    RiderId = table.Column<int>(type: "INTEGER", nullable: true),
                    HorseId = table.Column<int>(type: "INTEGER", nullable: true),
                    BackNumber = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    Placing = table.Column<int>(type: "INTEGER", nullable: true),
                    PlacingStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    Score = table.Column<decimal>(type: "TEXT", precision: 9, scale: 3, nullable: true),
                    ScoreNote = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Points = table.Column<decimal>(type: "TEXT", precision: 9, scale: 3, nullable: true),
                    PointsSource = table.Column<int>(type: "INTEGER", nullable: false),
                    ImportBatchId = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Results", x => x.Id);
                    table.CheckConstraint("CK_Result_RiderOrHorse", "\"RiderId\" IS NOT NULL OR \"HorseId\" IS NOT NULL");
                    table.ForeignKey(
                        name: "FK_Results_Horses_HorseId",
                        column: x => x.HorseId,
                        principalTable: "Horses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Results_ImportBatches_ImportBatchId",
                        column: x => x.ImportBatchId,
                        principalTable: "ImportBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Results_Riders_RiderId",
                        column: x => x.RiderId,
                        principalTable: "Riders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Results_ShowClasses_ShowClassId",
                        column: x => x.ShowClassId,
                        principalTable: "ShowClasses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Divisions_PointScheduleId",
                table: "Divisions",
                column: "PointScheduleId");

            migrationBuilder.CreateIndex(
                name: "IX_Divisions_SeasonId_Name",
                table: "Divisions",
                columns: new[] { "SeasonId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HorseAliases_Alias",
                table: "HorseAliases",
                column: "Alias",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HorseAliases_HorseId",
                table: "HorseAliases",
                column: "HorseId");

            migrationBuilder.CreateIndex(
                name: "IX_Horses_Name",
                table: "Horses",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_ImportBatches_MappingProfileId",
                table: "ImportBatches",
                column: "MappingProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_ImportBatches_RawContentHash",
                table: "ImportBatches",
                column: "RawContentHash");

            migrationBuilder.CreateIndex(
                name: "IX_ImportBatches_SeasonId",
                table: "ImportBatches",
                column: "SeasonId");

            migrationBuilder.CreateIndex(
                name: "IX_ImportBatches_ShowId",
                table: "ImportBatches",
                column: "ShowId");

            migrationBuilder.CreateIndex(
                name: "IX_MappingProfiles_Name",
                table: "MappingProfiles",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PointScheduleRows_PointScheduleId_Placing_MinEntries",
                table: "PointScheduleRows",
                columns: new[] { "PointScheduleId", "Placing", "MinEntries" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PointSchedules_SeasonId",
                table: "PointSchedules",
                column: "SeasonId");

            migrationBuilder.CreateIndex(
                name: "IX_Results_HorseId",
                table: "Results",
                column: "HorseId");

            migrationBuilder.CreateIndex(
                name: "IX_Results_ImportBatchId",
                table: "Results",
                column: "ImportBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_Results_RiderId",
                table: "Results",
                column: "RiderId");

            migrationBuilder.CreateIndex(
                name: "IX_Results_ShowClassId",
                table: "Results",
                column: "ShowClassId");

            migrationBuilder.CreateIndex(
                name: "IX_RiderAliases_Alias",
                table: "RiderAliases",
                column: "Alias",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RiderAliases_RiderId",
                table: "RiderAliases",
                column: "RiderId");

            migrationBuilder.CreateIndex(
                name: "IX_Riders_Name",
                table: "Riders",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Seasons_DefaultPointScheduleId",
                table: "Seasons",
                column: "DefaultPointScheduleId");

            migrationBuilder.CreateIndex(
                name: "IX_Seasons_Year",
                table: "Seasons",
                column: "Year");

            migrationBuilder.CreateIndex(
                name: "IX_ShowClasses_DivisionId",
                table: "ShowClasses",
                column: "DivisionId");

            migrationBuilder.CreateIndex(
                name: "IX_ShowClasses_ShowId_SortOrder",
                table: "ShowClasses",
                columns: new[] { "ShowId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_Shows_SeasonId_StartDate",
                table: "Shows",
                columns: new[] { "SeasonId", "StartDate" });

            migrationBuilder.AddForeignKey(
                name: "FK_Divisions_PointSchedules_PointScheduleId",
                table: "Divisions",
                column: "PointScheduleId",
                principalTable: "PointSchedules",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Divisions_Seasons_SeasonId",
                table: "Divisions",
                column: "SeasonId",
                principalTable: "Seasons",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ImportBatches_Seasons_SeasonId",
                table: "ImportBatches",
                column: "SeasonId",
                principalTable: "Seasons",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ImportBatches_Shows_ShowId",
                table: "ImportBatches",
                column: "ShowId",
                principalTable: "Shows",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_PointScheduleRows_PointSchedules_PointScheduleId",
                table: "PointScheduleRows",
                column: "PointScheduleId",
                principalTable: "PointSchedules",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PointSchedules_Seasons_SeasonId",
                table: "PointSchedules",
                column: "SeasonId",
                principalTable: "Seasons",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Seasons_PointSchedules_DefaultPointScheduleId",
                table: "Seasons");

            migrationBuilder.DropTable(
                name: "HorseAliases");

            migrationBuilder.DropTable(
                name: "PointScheduleRows");

            migrationBuilder.DropTable(
                name: "Results");

            migrationBuilder.DropTable(
                name: "RiderAliases");

            migrationBuilder.DropTable(
                name: "Horses");

            migrationBuilder.DropTable(
                name: "ImportBatches");

            migrationBuilder.DropTable(
                name: "ShowClasses");

            migrationBuilder.DropTable(
                name: "Riders");

            migrationBuilder.DropTable(
                name: "MappingProfiles");

            migrationBuilder.DropTable(
                name: "Divisions");

            migrationBuilder.DropTable(
                name: "Shows");

            migrationBuilder.DropTable(
                name: "PointSchedules");

            migrationBuilder.DropTable(
                name: "Seasons");
        }
    }
}
