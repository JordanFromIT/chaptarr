using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using FluentValidation.Results;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.MediaFiles;

namespace NzbDrone.Core.Notifications.Kavita;

public class Kavita : NotificationBase<KavitaSettings>
{
    private readonly IKavitaService _kavitaService;
    private readonly Logger _logger;

    public Kavita(IKavitaService kavitaService, Logger logger)
    {
        _kavitaService = kavitaService;
        _logger = logger;
    }

    public override string Link => "https://www.kavitareader.com/";

    public override void OnReleaseImport(BookDownloadMessage message)
    {
        Notify(GetBookFolder(message.BookFiles));
    }

    public override void OnBookDelete(BookDeleteMessage deleteMessage)
    {
        Notify(GetBookFolder(deleteMessage.Book?.BookFiles));
    }

    public override void OnBookFileDelete(BookFileDeleteMessage message)
    {
        Notify(GetBookFolder(new[] { message.BookFile }));
    }

    public override void OnBookRetag(BookRetagMessage message)
    {
        Notify(GetBookFolder(new[] { message.BookFile }));
    }

    public override string Name => "Kavita";

    public override ValidationResult Test()
    {
        var failures = new List<ValidationFailure>();

        failures.AddIfNotNull(_kavitaService.Test(Settings, "Success! Kavita has been successfully configured!"));

        return new ValidationResult(failures);
    }

    // A book that never had a file (author refresh prunes plenty of those) has no folder for Kavita to
    // scan. Returning null here instead of throwing keeps Chaptarr from recording a failed notification
    // and backing the Kavita connection off.
    private static string GetBookFolder(IEnumerable<BookFile> bookFiles)
    {
        var firstPath = bookFiles?.Select(v => v?.Path).FirstOrDefault(p => p.IsNotNullOrWhiteSpace());

        return firstPath == null ? null : Directory.GetParent(firstPath)?.FullName;
    }

    private void Notify(string folderPath)
    {
        try
        {
            // Kavita's scan-folder endpoint takes the folder to scan and nothing else. Prefixing it with a
            // notification title makes Kavita answer 500 and the new book only shows up on its nightly scan.
            if (Settings.Notify && folderPath.IsNotNullOrWhiteSpace())
            {
                _kavitaService.Notify(Settings, folderPath);
            }
        }
        catch (SocketException ex)
        {
            var logMessage = $"Unable to connect to Subsonic Host: {Settings.Host}:{Settings.Port}";
            _logger.Debug(ex, logMessage);
        }
    }
}
