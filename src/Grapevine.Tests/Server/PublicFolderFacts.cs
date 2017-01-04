﻿using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Grapevine.Server;
using Grapevine.Shared;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Grapevine.Tests.Server
{
    public class PublicFolderFacts
    {
        private static readonly Random Random = new Random();
        private static string GenerateUniqueString()
        {
            return Guid.NewGuid().Truncate() + "-" + Random.Next(10,99);
        }

        private static void CleanUp(string folderpath)
        {
            try
            {
                foreach (var file in Directory.GetFiles(folderpath))
                {
                    File.Delete(file);
                }

                Directory.Delete(folderpath);
            }
            catch { /* ignored */ }
        }

        public class Constructors
        {
            [Fact]
            public void NoParameters()
            {
                var folder = new PublicFolder();
                folder.IndexFileName.ShouldBe(PublicFolder.DefaultIndexFileName);
                folder.FolderPath.ShouldBe(Path.Combine(Directory.GetCurrentDirectory(), PublicFolder.DefaultFolderName));
                folder.Prefix.Equals(string.Empty).ShouldBeTrue();
                folder.DirectoryListing.Any().ShouldBeFalse();
            }

            [Fact]
            public void AbsolutePathShouldNotChange()
            {
                const string path = @"C:\temp";

                var folder = new PublicFolder(path);

                folder.IndexFileName.ShouldBe(PublicFolder.DefaultIndexFileName);
                folder.FolderPath.ShouldBe(path);
                folder.Prefix.Equals(string.Empty).ShouldBeTrue();
                folder.DirectoryListing.Any().ShouldBeFalse();

                CleanUp(folder.FolderPath);
            }

            [Fact]
            public void RelativePathShouldBeRelativeToCurrentFolder()
            {
                const string path = "temp";

                var folder = new PublicFolder(path);

                folder.IndexFileName.ShouldBe(PublicFolder.DefaultIndexFileName);
                folder.FolderPath.ShouldBe(Path.Combine(Directory.GetCurrentDirectory(), path));
                folder.Prefix.Equals(string.Empty).ShouldBeTrue();
                folder.DirectoryListing.Any().ShouldBeFalse();

                CleanUp(folder.FolderPath);
            }

            [Fact]
            public void PathAndPrefixSetsPrefix()
            {
                var path = Path.Combine(Directory.GetCurrentDirectory(), GenerateUniqueString());
                const string prefix = "testing";

                var folder = new PublicFolder(path, prefix);

                folder.IndexFileName.ShouldBe(PublicFolder.DefaultIndexFileName);
                folder.FolderPath.ShouldBe(path);
                folder.Prefix.Equals($"/{prefix}").ShouldBeTrue();
                folder.DirectoryListing.Any().ShouldBeFalse();

                CleanUp(folder.FolderPath);
            }
        }

        public class DefaultFileNameProperty
        {
            [Fact]
            public void DefaultFileNameShowsInDirectoryListingForFileAndFolder()
            {
                var path = Path.Combine(Directory.GetCurrentDirectory(), GenerateUniqueString());

                Directory.CreateDirectory(path);
                File.WriteAllText(Path.Combine(path, PublicFolder.DefaultIndexFileName), "for testing purposes - delete me");

                var folder = new PublicFolder(path);

                folder.DirectoryListing.Count.ShouldBe(2);
                folder.DirectoryListing.Count(x => x.Key.EndsWith(PublicFolder.DefaultIndexFileName)).ShouldBe(1);
                folder.DirectoryListing.Count(x => x.Value == Path.Combine(folder.FolderPath, PublicFolder.DefaultIndexFileName)).ShouldBe(2);

                CleanUp(folder.FolderPath);
            }

            [Fact]
            public void DefaultFileNameDoesNotChangeWhenSetToEmptyString()
            {
                var folder = new PublicFolder();

                var defaultFileName = folder.IndexFileName;
                folder.IndexFileName = string.Empty;

                folder.IndexFileName.ShouldBe(defaultFileName);
            }

            [Fact]
            public void DefaultFileNameDoesNotChangeWhenSetToNull()
            {
                var folder = new PublicFolder();

                var defaultFileName = folder.IndexFileName;
                folder.IndexFileName = null;

                folder.IndexFileName.ShouldBe(defaultFileName);
            }

            [Fact]
            public void DirectoryListingIsUpdatedWhenDefaultFileNameChanges()
            {
                var defaultFileName1 = PublicFolder.DefaultIndexFileName;
                const string defaultFileName2 = "default.html";

                var path = Path.Combine(Directory.GetCurrentDirectory(), GenerateUniqueString());

                Directory.CreateDirectory(path);
                File.WriteAllText(Path.Combine(path, defaultFileName1), "for testing purposes - delete me");
                File.WriteAllText(Path.Combine(path, defaultFileName2), "for testing purposes - delete me");

                var folder = new PublicFolder(path);

                folder.DirectoryListing.Count.ShouldBe(3);
                folder.DirectoryListing.Count(x => x.Key.EndsWith(defaultFileName1)).ShouldBe(1);
                folder.DirectoryListing.Count(x => x.Key.EndsWith(defaultFileName2)).ShouldBe(1);
                folder.DirectoryListing.Count(x => x.Value == Path.Combine(folder.FolderPath, defaultFileName1)).ShouldBe(2);

                folder.IndexFileName = defaultFileName2;

                folder.DirectoryListing.Count.ShouldBe(3);
                folder.DirectoryListing.Count(x => x.Key.EndsWith(defaultFileName2)).ShouldBe(1);
                folder.DirectoryListing.Count(x => x.Key.EndsWith(defaultFileName2)).ShouldBe(1);
                folder.DirectoryListing.Count(x => x.Value == Path.Combine(folder.FolderPath, defaultFileName2)).ShouldBe(2);

                CleanUp(folder.FolderPath);
            }
        }

        public class FileSystemWatcherProperty
        {
            [Fact]
            public void CanProvideCustomWatcher()
            {
                var folder = new PublicFolder();
                var watcher = new FileSystemWatcher
                {
                    Path = folder.FolderPath,
                    Filter = "*.jpg",
                    EnableRaisingEvents = true,
                    IncludeSubdirectories = true,
                    NotifyFilter = NotifyFilters.FileName
                };

                folder.Watcher.Filter.ShouldNotBe(watcher.Filter);

                folder.Watcher = watcher;

                folder.Watcher.Filter.ShouldBe(watcher.Filter);
            }

            [Fact]
            public void DoesNotUpdateToNull()
            {
                var folder = new PublicFolder();
                var watcher = folder.Watcher;

                folder.Watcher = null;

                folder.Watcher.ShouldNotBeNull();
                folder.Watcher.Equals(watcher).ShouldBeTrue();
            }

            [Fact]
            public void DoesNotDisposeWhenSetToSameValue()
            {
                var watcher = Substitute.For<FileSystemWatcher>();
                var folder = new PublicFolder {Watcher =  watcher};

                folder.Watcher = watcher;

                watcher.DidNotReceive().Dispose();
            }
        }

        public class FolderPathProperty
        {
            [Fact]
            public void CreatesFolderIfNotExists()
            {
                var path = Path.Combine(Directory.GetCurrentDirectory(), GenerateUniqueString());
                Directory.Exists(path).ShouldBeFalse();

                var folder = new PublicFolder(path);

                Directory.Exists(path).ShouldBeTrue();

                CleanUp(folder.FolderPath);
            }
        }

        public class PrefixProperty
        {
            [Fact]
            public void IsEmptyStringWhenSetToNull()
            {
                var folder = new PublicFolder {Prefix = null};
                folder.Prefix.Equals(string.Empty).ShouldBeTrue();
            }

            [Fact]
            public void PrependsMissingForwardSlash()
            {
                var folder = new PublicFolder {Prefix = "hello"};
                folder.Prefix.Equals("hello").ShouldBeFalse();
                folder.Prefix.Equals("/hello").ShouldBeTrue();
            }

            [Fact]
            public void DoesNotPrependForwadSlashWhenExists()
            {
                var folder = new PublicFolder {Prefix = "/hello"};
                folder.Prefix.Equals("/hello").ShouldBeTrue();
            }

            [Fact]
            public void TrimsTrailingSlash()
            {
                var folder = new PublicFolder {Prefix = "hello/"};
                folder.Prefix.Equals("/hello").ShouldBeTrue();

                folder.Prefix = "/hello/";
                folder.Prefix.Equals("/hello").ShouldBeTrue();
            }

            [Fact]
            public void TrimsLeadingAndTrailingWhitespace()
            {
                var folder = new PublicFolder {Prefix = "  /hello/  "};
                folder.Prefix.Equals("/hello").ShouldBeTrue();
            }
        }

        public class CreateDirectoryListKeyMethod
        {
            [Fact]
            public void ReplacesBackslashWithForwardslash()
            {
                var folder = new PublicFolder();
                var path = Path.Combine(folder.FolderPath, "path1", "path2");

                path.Contains(@"\").ShouldBeTrue();
                path.Contains(@"/").ShouldBeFalse();

                path = folder.GetDirectoryListKey(path);

                path.Contains(@"\").ShouldBeFalse();
                path.Contains(@"/").ShouldBeTrue();
            }

            [Fact]
            public void RemovesFolderPath()
            {
                var folder = new PublicFolder();
                var path = Path.Combine(folder.FolderPath, "path1", "path2");

                path.StartsWith(folder.FolderPath).ShouldBeTrue();
                path.ToLower().StartsWith(folder.FolderPath.ToLower()).ShouldBeTrue();

                path = folder.GetDirectoryListKey(path);

                path.Equals("/path1/path2").ShouldBeTrue();
            }

            [Fact]
            public void AppendsPrefix()
            {
                var folder = new PublicFolder { Prefix = "unit-test" };
                var path = folder.GetDirectoryListKey(Path.Combine(folder.FolderPath, "path1", "path2"));

                path.Equals("/unit-test/path1/path2").ShouldBeTrue();
            }
        }

        public class DisposeMethod
        {
            [Fact]
            public void DisposesOfFileSystemWatcher()
            {
                var watcher = Substitute.For<FileSystemWatcher>();
                var folder = new PublicFolder { Watcher = watcher };

                folder.Dispose();

                watcher.Received().Dispose();
            }
        }

        public class FileSystemWatcherEventHandlers
        {
            [Fact]
            public void AddsNewFilesToList()
            {
                var updated = new ManualResetEvent(false);

                var path = Path.Combine(Directory.GetCurrentDirectory(), GenerateUniqueString());
                Directory.CreateDirectory(path);

                var folder = new PublicFolder(path);
                folder.DirectoryListing.Count.ShouldBe(0);
                folder.Watcher.Created += (sender, args) => { updated.Set(); };

                var filename = GenerateUniqueString();
                var filepath = Path.Combine(path, filename);
                File.WriteAllText(filepath, "for testing purposes - delete me");

                updated.WaitOne(1000, false);

                folder.DirectoryListing.Count.ShouldBe(1);
                folder.DirectoryListing.Count(x => x.Key == $"/{filename}" && x.Value == filepath).ShouldBe(1);

                CleanUp(folder.FolderPath);
            }

            [Fact]
            public void RemovesDeletedFilesFromList()
            {
                var updated = new ManualResetEvent(false);

                var path = Path.Combine(Directory.GetCurrentDirectory(), GenerateUniqueString());
                Directory.CreateDirectory(path);

                var filepath = Path.Combine(path, GenerateUniqueString());
                File.WriteAllText(filepath, "for testing purposes - delete me");

                var folder = new PublicFolder(path);
                folder.DirectoryListing.Count.ShouldBe(1);
                folder.Watcher.Deleted += (sender, args) => { updated.Set(); };

                File.Delete(filepath);
                updated.WaitOne(1000, false);

                folder.DirectoryListing.Count.ShouldBe(0);

                CleanUp(folder.FolderPath);
            }

            [Fact]
            public void ChangesNamesOfRenamedFilesInList()
            {
                var updated = new ManualResetEvent(false);

                var path = Path.Combine(Directory.GetCurrentDirectory(), GenerateUniqueString());
                Directory.CreateDirectory(path);

                var filepath = Path.Combine(path, GenerateUniqueString());
                var newfilepath = Path.Combine(path, GenerateUniqueString());
                File.WriteAllText(filepath, "for testing purposes - delete me");

                var folder = new PublicFolder(path);
                folder.DirectoryListing.Count.ShouldBe(1);
                folder.DirectoryListing.Count(x => x.Value == filepath).ShouldBe(1);
                folder.Watcher.Renamed += (sender, args) => { updated.Set(); };

                File.Move(filepath, newfilepath);
                updated.WaitOne(1000, false);

                folder.DirectoryListing.Count.ShouldBe(1);
                folder.DirectoryListing.Count(x => x.Value == newfilepath).ShouldBe(1);

                CleanUp(folder.FolderPath);
            }

            [Fact]
            public void AddsTwoFilesToListWhenAddingDefaultFileName()
            {
                var updated = new ManualResetEvent(false);

                var path = Path.Combine(Directory.GetCurrentDirectory(), GenerateUniqueString());
                Directory.CreateDirectory(path);

                var folder = new PublicFolder(path);
                folder.DirectoryListing.Count.ShouldBe(0);
                folder.Watcher.Created += (sender, args) => { updated.Set(); };

                var filename = folder.IndexFileName;
                var filepath = Path.Combine(path, filename);
                File.WriteAllText(filepath, "for testing purposes - delete me");

                updated.WaitOne(1000, false);

                folder.DirectoryListing.Count.ShouldBe(2);
                folder.DirectoryListing.Count(x => x.Key == $"/{filename}" && x.Value == filepath).ShouldBe(1);

                CleanUp(folder.FolderPath);
            }

            [Fact]
            public void RemovesTwoFilesFromListWhenRemovingDefaultFileName()
            {
                var updated = new ManualResetEvent(false);

                var path = Path.Combine(Directory.GetCurrentDirectory(), GenerateUniqueString());
                Directory.CreateDirectory(path);

                var filepath = Path.Combine(path, PublicFolder.DefaultIndexFileName);
                File.WriteAllText(filepath, "for testing purposes - delete me");
                File.WriteAllText(Path.Combine(path, GenerateUniqueString()), "for testing purposes - delete me");

                var folder = new PublicFolder(path);
                folder.DirectoryListing.Count.ShouldBe(3);
                folder.DirectoryListing.Count(x => x.Value == Path.Combine(path, PublicFolder.DefaultIndexFileName)).ShouldBe(2);
                folder.DirectoryListing.Count(x => x.Key == $"/{PublicFolder.DefaultIndexFileName}").ShouldBe(1);
                folder.Watcher.Deleted += (sender, args) => { updated.Set(); };

                File.Delete(filepath);
                updated.WaitOne(1000, false);

                folder.DirectoryListing.Count.ShouldBe(1);

                CleanUp(folder.FolderPath);
            }

            [Fact]
            public void UpdatesIndexerWhenChangingToDefaultFileName()
            {
                var updated = new ManualResetEvent(false);

                var path = Path.Combine(Directory.GetCurrentDirectory(), GenerateUniqueString());
                Directory.CreateDirectory(path);

                var filepath = Path.Combine(path, GenerateUniqueString());
                var newfilepath = Path.Combine(path, PublicFolder.DefaultIndexFileName);
                File.WriteAllText(filepath, "for testing purposes - delete me");

                var folder = new PublicFolder(path);
                folder.DirectoryListing.Count.ShouldBe(1);
                folder.DirectoryListing.Count(x => x.Value == filepath).ShouldBe(1);
                folder.Watcher.Renamed += (sender, args) => { updated.Set(); };

                File.Move(filepath, newfilepath);
                updated.WaitOne(1000, false);

                folder.DirectoryListing.Count.ShouldBe(2);
                folder.DirectoryListing.Count(x => x.Value == newfilepath).ShouldBe(2);
                folder.DirectoryListing.Count(x => x.Key == $"/{PublicFolder.DefaultIndexFileName}").ShouldBe(1);

                CleanUp(folder.FolderPath);
            }

            [Fact]
            public void UpdatesIndexerWhenChangingFromDefaultFileName()
            {
                var updated = new ManualResetEvent(false);

                var path = Path.Combine(Directory.GetCurrentDirectory(), GenerateUniqueString());
                Directory.CreateDirectory(path);

                var filepath = Path.Combine(path, PublicFolder.DefaultIndexFileName);
                var newfilepath = Path.Combine(path, GenerateUniqueString());
                File.WriteAllText(filepath, "for testing purposes - delete me");

                var folder = new PublicFolder(path);
                folder.DirectoryListing.Count.ShouldBe(2);
                folder.DirectoryListing.Count(x => x.Value == filepath).ShouldBe(2);
                folder.DirectoryListing.Count(x => x.Key == $"/{PublicFolder.DefaultIndexFileName}").ShouldBe(1);
                folder.Watcher.Renamed += (sender, args) => { updated.Set(); };

                File.Move(filepath, newfilepath);
                updated.WaitOne(1000, false);

                folder.DirectoryListing.Count.ShouldBe(1);
                folder.DirectoryListing.Count(x => x.Value == newfilepath).ShouldBe(1);
                folder.DirectoryListing.Count(x => x.Key == $"/{PublicFolder.DefaultIndexFileName}").ShouldBe(0);

                CleanUp(folder.FolderPath);
            }
        }

        public class SendFileMethod
        {
            [Fact]
            public void CallsSendResponseWhenFileExists()
            {
                var filename = GenerateUniqueString();

                var folder = new PublicFolder(GenerateUniqueString());
                var file = Path.Combine(folder.FolderPath, filename);
                File.WriteAllText(file, "for testing purposes - delete me");

                var context = Mocks.HttpContext();
                context.Request.PathInfo.Returns($"/{filename}");
                context.Response.When(x => x.SendResponse(Arg.Any<string>(), true)).Do(info =>
                {
                    context.WasRespondedTo.Returns(true);
                });

                folder.SendFile(context);

                context.WasRespondedTo.ShouldBeTrue();

                CleanUp(folder.FolderPath);
            }

            [Fact]
            public void DoesNotCallSendResponseWhenFileDoesNotExist()
            {
                var context = Mocks.HttpContext();
                context.Request.PathInfo.Returns($"/{GenerateUniqueString()}");
                context.Response.When(x => x.SendResponse(Arg.Any<string>(), true)).Do(info =>
                {
                    context.WasRespondedTo.Returns(true);
                });

                var folder = new PublicFolder();
                folder.SendFile(context);

                context.WasRespondedTo.ShouldBeFalse();
            }

            [Fact]
            public void ThrowsExceptionWhenFileShouldExist()
            {
                var prefix = GenerateUniqueString();
                var context = Mocks.HttpContext();
                context.Request.PathInfo.Returns($"/{prefix}/{GenerateUniqueString()}");
                context.Response.When(x => x.SendResponse(Arg.Any<string>(), true)).Do(info =>
                {
                    context.WasRespondedTo.Returns(true);
                });

                var folder = new PublicFolder {Prefix = prefix};

                Should.Throw<Exceptions.Server.FileNotFoundException>(() => folder.SendFile(context));
            }
        }
    }

    public static class PublicFolderExtensions
    {
        internal static string GetDirectoryListKey(this PublicFolder folder, string item)
        {
            var method = folder.GetType().GetMethod("CreateDirectoryListKey", BindingFlags.Instance | BindingFlags.NonPublic);
            var result = method.Invoke(folder, new object[] { item });
            return (string)result;
        }
    }
}
