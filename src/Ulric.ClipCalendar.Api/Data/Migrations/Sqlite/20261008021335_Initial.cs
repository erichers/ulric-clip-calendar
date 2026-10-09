using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ulric.ClipCalendar.Api.Data.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Brands",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    Slug = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    Color = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    Instagram = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    TikTok = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    YouTube = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    Facebook = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    DefaultHashtags = table.Column<string>(type: "TEXT", maxLength: 400, nullable: false),
                    CadenceLabel = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    CadenceDays = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    DefaultPostTime = table.Column<TimeOnly>(type: "TEXT", nullable: false),
                    Latitude = table.Column<double>(type: "REAL", nullable: false),
                    Longitude = table.Column<double>(type: "REAL", nullable: false),
                    LocationLabel = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Brands", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Clips",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    BrandId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Caption = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    Hashtags = table.Column<string>(type: "TEXT", maxLength: 400, nullable: false),
                    PlatformsCsv = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Series = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    SeriesPart = table.Column<int>(type: "INTEGER", nullable: true),
                    PostDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    PostTime = table.Column<TimeOnly>(type: "TEXT", nullable: false),
                    StoriesOk = table.Column<bool>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    SourceKind = table.Column<int>(type: "INTEGER", nullable: false),
                    SourceLink = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    OriginalFileName = table.Column<string>(type: "TEXT", maxLength: 260, nullable: true),
                    OriginalPath = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    ProcessedPath = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    ThumbnailPath = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    DurationSeconds = table.Column<double>(type: "REAL", nullable: true),
                    Width = table.Column<int>(type: "INTEGER", nullable: true),
                    Height = table.Column<int>(type: "INTEGER", nullable: true),
                    VideoCodec = table.Column<string>(type: "TEXT", maxLength: 40, nullable: true),
                    TrimStartSeconds = table.Column<double>(type: "REAL", nullable: true),
                    TrimEndSeconds = table.Column<double>(type: "REAL", nullable: true),
                    MediaState = table.Column<int>(type: "INTEGER", nullable: false),
                    MediaMessage = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Clips", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Clips_Brands_BrandId",
                        column: x => x.BrandId,
                        principalTable: "Brands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ShareLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Token = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    BrandId = table.Column<Guid>(type: "TEXT", nullable: true),
                    RangeStart = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    RangeEnd = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    Label = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShareLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShareLinks_Brands_BrandId",
                        column: x => x.BrandId,
                        principalTable: "Brands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Comments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ClipId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Author = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Body = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Comments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Comments_Clips_ClipId",
                        column: x => x.ClipId,
                        principalTable: "Clips",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StatusEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ClipId = table.Column<Guid>(type: "TEXT", nullable: false),
                    FromStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    ToStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    Note = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    Actor = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StatusEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StatusEvents_Clips_ClipId",
                        column: x => x.ClipId,
                        principalTable: "Clips",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Brands_Slug",
                table: "Brands",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Clips_BrandId_PostDate",
                table: "Clips",
                columns: new[] { "BrandId", "PostDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Comments_ClipId",
                table: "Comments",
                column: "ClipId");

            migrationBuilder.CreateIndex(
                name: "IX_ShareLinks_BrandId",
                table: "ShareLinks",
                column: "BrandId");

            migrationBuilder.CreateIndex(
                name: "IX_ShareLinks_Token",
                table: "ShareLinks",
                column: "Token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StatusEvents_ClipId",
                table: "StatusEvents",
                column: "ClipId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Comments");

            migrationBuilder.DropTable(
                name: "ShareLinks");

            migrationBuilder.DropTable(
                name: "StatusEvents");

            migrationBuilder.DropTable(
                name: "Clips");

            migrationBuilder.DropTable(
                name: "Brands");
        }
    }
}
