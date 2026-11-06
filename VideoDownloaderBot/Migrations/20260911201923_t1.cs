using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VideoDownloaderBot.Migrations
{
    /// <inheritdoc />
    public partial class t1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AudioFileName",
                table: "DownloadHistories",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "AudioFileSize",
                table: "DownloadHistories",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AudioPath",
                table: "DownloadHistories",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AudioFileName",
                table: "DownloadHistories");

            migrationBuilder.DropColumn(
                name: "AudioFileSize",
                table: "DownloadHistories");

            migrationBuilder.DropColumn(
                name: "AudioPath",
                table: "DownloadHistories");
        }
    }
}
