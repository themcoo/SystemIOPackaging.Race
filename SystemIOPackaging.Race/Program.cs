using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Packaging;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;

namespace SystemIOPackaging.Race
{
    class Program
    {
        static void Main(string[] args)
        {
            const int parallelPackages = 2;

            var packageCloseSemaphore = new SemaphoreSlim(0);
            var tasks = new Task[parallelPackages];
            MemoryStream oneOfPackagesData = null;

            for (var i = 0; i < parallelPackages; i++)
            {
                var (package, str) = CreateOpenXml();
                tasks[i] = RunTask(packageCloseSemaphore, package);
                if (i == 0) oneOfPackagesData = str;
            }

            packageCloseSemaphore.Release(parallelPackages);

            Task.WaitAll(tasks);

            using var readStr = new MemoryStream(oneOfPackagesData!.ToArray());
            using var readPackage = Package.Open(readStr, FileMode.Open);

            var result = GetPartByRelationshipId(readPackage, "dummy");
            if (result is null)
            {
                Console.WriteLine("Race happened!");
                Environment.Exit(0);
            }

            Environment.Exit(1);
        }

        private static Task RunTask(SemaphoreSlim packageCloseSemaphore, Package package1)
        {
            var taskReady = new SemaphoreSlim(0);

            var task = Task.Run(() =>
            {
                taskReady.Release();
                packageCloseSemaphore.Wait();
                package1.Close();
            });

            taskReady.Wait();
            return task;
        }

        static (Package, MemoryStream) CreateOpenXml()
        {
            var memStr = new MemoryStream();
            var package = Package.Open(memStr, FileMode.Create);
            var partUri = new Uri($@"/subfolder/dummy.json", UriKind.Relative);
            var packagePart =
                package.CreatePart(partUri, MediaTypeNames.Application.Json, CompressionOption.NotCompressed);
            package.CreateRelationship(packagePart.Uri, TargetMode.Internal, "application^json", "dummy");
            return (package, memStr);
        }

        static PackagePart GetPartByRelationshipId(Package package, string id)
        {
            var relationshipExists = package.RelationshipExists(id);

            if (!relationshipExists)
            {
                return null;
            }

            var packageRelationship = package.GetRelationship(id);

            return package.GetPart(packageRelationship.TargetUri);
        }
    }
}