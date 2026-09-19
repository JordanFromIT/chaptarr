using System.Collections.Generic;
using System.IO;
using FluentValidation.Results;
using NLog;
using NUnit.Framework;
using NzbDrone.Core.Books;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.Notifications;
using NzbDrone.Core.Notifications.Kavita;
using KavitaNotification = NzbDrone.Core.Notifications.Kavita.Kavita;

namespace Chaptarr.Core.Test.Notifications.Kavita
{
    [TestFixture]
    public class KavitaFixture
    {
        private static readonly string BookFilePath = Path.Combine(Path.GetTempPath(), "ebooks", "Joseph Heller", "Catch-22", "Catch-22.epub");
        private static readonly string BookFolder = Directory.GetParent(BookFilePath).FullName;

        private sealed class RecordingKavitaService : IKavitaService
        {
            public List<string> FolderPaths { get; } = new List<string>();

            public void Notify(KavitaSettings settings, string folderPath) => FolderPaths.Add(folderPath);

            public ValidationFailure Test(KavitaSettings settings, string message) => null;
        }

        private static KavitaNotification CreateSubject(RecordingKavitaService service, bool notify = true)
        {
            return new KavitaNotification(service, LogManager.GetCurrentClassLogger())
            {
                Definition = new NotificationDefinition
                {
                    Settings = new KavitaSettings { Host = "kavita", Port = 5000, ApiKey = "key", Notify = notify }
                }
            };
        }

        [Test]
        public void should_send_kavita_only_the_book_folder_when_a_book_is_imported()
        {
            var service = new RecordingKavitaService();

            CreateSubject(service).OnReleaseImport(new BookDownloadMessage
            {
                BookFiles = new List<BookFile> { new BookFile { Path = BookFilePath } }
            });

            Assert.That(service.FolderPaths, Is.EqualTo(new[] { BookFolder }));
        }

        [Test]
        public void should_send_kavita_only_the_book_folder_when_a_book_file_is_deleted()
        {
            var service = new RecordingKavitaService();

            CreateSubject(service).OnBookFileDelete(new BookFileDeleteMessage { BookFile = new BookFile { Path = BookFilePath } });

            Assert.That(service.FolderPaths, Is.EqualTo(new[] { BookFolder }));
        }

        [Test]
        public void should_send_kavita_only_the_book_folder_when_a_book_is_retagged()
        {
            var service = new RecordingKavitaService();

            CreateSubject(service).OnBookRetag(new BookRetagMessage { BookFile = new BookFile { Path = BookFilePath } });

            Assert.That(service.FolderPaths, Is.EqualTo(new[] { BookFolder }));
        }

        [Test]
        public void should_not_put_the_notification_title_in_the_folder_path()
        {
            var service = new RecordingKavitaService();

            CreateSubject(service).OnReleaseImport(new BookDownloadMessage
            {
                BookFiles = new List<BookFile> { new BookFile { Path = BookFilePath } }
            });

            Assert.That(service.FolderPaths, Has.Count.EqualTo(1));
            Assert.That(service.FolderPaths[0], Does.Not.Contain("Chaptarr"));
            Assert.That(service.FolderPaths[0], Does.Not.Contain("Book Downloaded"));
        }

        [Test]
        public void should_send_kavita_only_the_book_folder_when_a_book_with_files_is_deleted()
        {
            var service = new RecordingKavitaService();
            var book = new Book
            {
                Title = "Catch-22",
                LazyBookFiles = new LazyLoaded<List<BookFile>>(new List<BookFile> { new BookFile { Path = BookFilePath } })
            };

            CreateSubject(service).OnBookDelete(new BookDeleteMessage(book, false));

            Assert.That(service.FolderPaths, Is.EqualTo(new[] { BookFolder }));
        }

        // Refreshing an author prunes book rows that never had a file. That raised "Sequence contains
        // no elements", which Chaptarr records as a failed notification and answers by switching the
        // Kavita connection off for a while.
        [Test]
        public void should_do_nothing_when_a_deleted_book_never_had_files()
        {
            var service = new RecordingKavitaService();
            var book = new Book { Title = "Closing Time", LazyBookFiles = new LazyLoaded<List<BookFile>>(new List<BookFile>()) };

            Assert.DoesNotThrow(() => CreateSubject(service).OnBookDelete(new BookDeleteMessage(book, false)));
            Assert.That(service.FolderPaths, Is.Empty);
        }

        [Test]
        public void should_do_nothing_when_a_deleted_book_has_no_file_list_at_all()
        {
            var service = new RecordingKavitaService();

            Assert.DoesNotThrow(() => CreateSubject(service).OnBookDelete(new BookDeleteMessage(new Book { Title = "Closing Time" }, false)));
            Assert.That(service.FolderPaths, Is.Empty);
        }

        [Test]
        public void should_do_nothing_when_an_import_message_carries_no_files()
        {
            var service = new RecordingKavitaService();

            Assert.DoesNotThrow(() => CreateSubject(service).OnReleaseImport(new BookDownloadMessage { BookFiles = new List<BookFile>() }));
            Assert.That(service.FolderPaths, Is.Empty);
        }

        [Test]
        public void should_not_call_kavita_when_library_updates_are_turned_off()
        {
            var service = new RecordingKavitaService();

            CreateSubject(service, notify: false).OnReleaseImport(new BookDownloadMessage
            {
                BookFiles = new List<BookFile> { new BookFile { Path = BookFilePath } }
            });

            Assert.That(service.FolderPaths, Is.Empty);
        }
    }
}
