using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ulric.ClipCalendar.Api.Data.Migrations.MySql
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Brands",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Name = table.Column<string>(type: "varchar(160)", maxLength: 160, nullable: false, collation: "utf8mb4_general_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Slug = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: false, collation: "utf8mb4_general_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Color = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false, collation: "utf8mb4_general_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Description = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: false, collation: "utf8mb4_general_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Instagram = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: false, collation: "utf8mb4_general_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TikTok = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: false, collation: "utf8mb4_general_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    YouTube = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: false, collation: "utf8mb4_general_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Facebook = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: false, collation: "utf8mb4_general_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DefaultHashtags = table.Column<string>(type: "varchar(400)", maxLength: 400, nullable: false, collation: "utf8mb4_general_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CadenceLabel = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: false, collation: "utf8mb4_general_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CadenceDays = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false, collation: "utf8mb4_general_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DefaultPostTime = table.Column<TimeOnly>(type: "time(6)", nullable: false),
                    Latitude = table.Column<double>(type: "double", nullable: false),
                    Longitude = table.Column<double>(type: "double", nullable: false),
                    LocationLabel = table.Column<string>(type: "varchar(160)", maxLength: 160, nullable: false, collation: "utf8mb4_general_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Brands", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_general_ci");

            migrationBuilder.CreateTable(
                name: "Clips",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    BrandId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Title = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false, collation: "utf8mb4_general_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Caption = table.Column<string>(type: "varchar(4000)", maxLength: 4000, nullable: false, collation: "utf8mb4_general_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Hashtags = table.Column<string>(type: "varchar(400)", maxLength: 400, nullable: false, collation: "utf8mb4_general_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PlatformsCsv = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false, collation: "utf8mb4_general_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Series = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false, collation: "utf8mb4_general_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SeriesPart = table.Column<int>(type: "int", nullable: true),
                    PostDate = table.Column<DateOnly>(type: "date", nullable: false),
                    PostTime = table.Column<TimeOnly>(type: "time(6)", nullable: false),
                    StoriesOk = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    SourceKind = table.Column<int>(type: "int", nullable: false),
                    SourceLink = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true, collation: "utf8mb4_general_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    OriginalFileName = table.Column<string>(type: "varchar(260)", maxLength: 260, nullable: true, collation: "utf8mb4_general_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    OriginalPath = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: true, collation: "utf8mb4_general_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ProcessedPath = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: true, collation: "utf8mb4_general_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ThumbnailPath = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: true, collation: "utf8mb4_general_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DurationSeconds = table.Column<double>(type: "double", nullable: true),
                    Width = table.Column<int>(type: "int", nullable: true),
                    Height = table.Column<int>(type: "int", nullable: true),
                    VideoCodec = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: true, collation: "utf8mb4_general_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TrimStartSeconds = table.Column<double>(type: "double", nullable: true),
                    TrimEndSeconds = table.Column<double>(type: "double", nullable: true),
                    MediaState = table.Column<int>(type: "int", nullable: false),
                    MediaMessage = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true, collation: "utf8mb4_general_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
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
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_general_ci");

            migrationBuilder.CreateTable(
                name: "ShareLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Token = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: false, collation: "utf8mb4_general_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    BrandId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    RangeStart = table.Column<DateOnly>(type: "date", nullable: true),
                    RangeEnd = table.Column<DateOnly>(type: "date", nullable: true),
                    Label = table.Column<string>(type: "varchar(160)", maxLength: 160, nullable: false, collation: "utf8mb4_general_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
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
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_general_ci");

            migrationBuilder.CreateTable(
                name: "Comments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ClipId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Author = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false, collation: "utf8mb4_general_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Body = table.Column<string>(type: "varchar(4000)", maxLength: 4000, nullable: false, collation: "utf8mb4_general_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
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
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_general_ci");

            migrationBuilder.CreateTable(
                name: "StatusEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ClipId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    FromStatus = table.Column<int>(type: "int", nullable: false),
                    ToStatus = table.Column<int>(type: "int", nullable: false),
                    Note = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true, collation: "utf8mb4_general_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Actor = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false, collation: "utf8mb4_general_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
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
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_general_ci");

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
