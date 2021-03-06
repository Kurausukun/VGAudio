﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Cake.Common.IO;
using Cake.Common.Tools.DotNetCore;
using Cake.Common.Tools.DotNetCore.Pack;
using Cake.Common.Tools.DotNetCore.Publish;
using Cake.Core;
using Cake.Core.IO;
using Cake.Frosting;
using ILRepacking;
using static Build.Utilities;

namespace Build.Tasks
{
    [Dependency(typeof(BuildLibrary))]
    public sealed class PublishLibrary : FrostingTask<Context>
    {
        public override void Run(Context context)
        {
            string[] frameworks = context.LibBuilds.Values
                .Where(x => x.LibSuccess == true)
                .Select(x => x.LibFramework)
                .ToArray();

            context.DotNetCorePack(context.LibraryDir.FullPath, new DotNetCorePackSettings
            {
                Configuration = context.Configuration,
                OutputDirectory = context.LibraryBinDir,
                NoBuild = true,
                IncludeSource = true,
                IncludeSymbols = true,
                ArgumentCustomization = args => args.Append($"/p:TargetFrameworks=\\\"{string.Join(";", frameworks)}\\\"")
            });
        }

        public override bool ShouldRun(Context context) =>
            context.LibBuilds.Values.Any(x => x.LibSuccess == true);
    }

    [Dependency(typeof(BuildCli))]
    public sealed class PublishCli : FrostingTask<Context>
    {
        public override void Run(Context context)
        {
            IEnumerable<LibraryBuildStatus> builds = context.LibBuilds.Values
                .Where(x => x.CliSuccess == true);

            foreach (LibraryBuildStatus build in builds)
            {
                context.DotNetCorePublish(context.CliDir.FullPath, new DotNetCorePublishSettings
                {
                    Framework = build.CliFramework,
                    Configuration = context.Configuration,
                    OutputDirectory = context.CliBinDir.Combine(build.CliFramework)
                });
            }
            DeleteFile(context,
                context.CliBinDir.Combine(context.LibBuilds["full"].CliFramework)
                    .CombineWithFilePath("VGAudioCli.runtimeconfig.json"), false);
        }

        public override bool ShouldRun(Context context) =>
            context.LibBuilds.Values.Any(x => x.CliSuccess == true);
    }

    [Dependency(typeof(BuildTools))]
    public sealed class PublishTools : FrostingTask<Context>
    {
        public override void Run(Context context)
        {
            IEnumerable<LibraryBuildStatus> builds = context.LibBuilds.Values
                .Where(x => x.ToolsSuccess == true);

            foreach (LibraryBuildStatus build in builds)
            {
                context.DotNetCorePublish(context.ToolsDir.FullPath, new DotNetCorePublishSettings
                {
                    Framework = build.ToolsFramework,
                    Configuration = context.Configuration,
                    OutputDirectory = context.CliBinDir.Combine(build.ToolsFramework)
                });
            }
            DeleteFile(context, context.CliBinDir.Combine(context.LibBuilds["full"].ToolsFramework).CombineWithFilePath("VGAudioTools.runtimeconfig.json"), false);
        }

        public override bool ShouldRun(Context context) =>
            context.LibBuilds.Values.Any(x => x.ToolsSuccess == true);
    }

    [Dependency(typeof(PublishCli))]
    public sealed class IlRepackCli : FrostingTask<Context>
    {
        public override void Run(Context context)
        {
            RepackOptions options = new RepackOptions
            {
                OutputFile = context.CliBinDir.CombineWithFilePath("VGAudioCli.exe").FullPath,
                InputAssemblies = new[]
                {
                    context.CliBinDir.CombineWithFilePath($"{context.LibBuilds["full"].CliFramework}/VGAudioCli.exe").FullPath,
                    context.CliBinDir.CombineWithFilePath($"{context.LibBuilds["full"].CliFramework}/VGAudio.dll").FullPath
                },
                SearchDirectories = new[] { "." }
            };
            new ILRepack(options).Repack();
        }

        public override bool ShouldRun(Context context) =>
            context.LibBuilds["full"].CliSuccess == true;

        public override void OnError(Exception exception, Context context) =>
            DisplayError(context, "Error creating merged assembly:\n" + exception.Message);
    }

    [Dependency(typeof(PublishTools))]
    public sealed class IlRepackTools : FrostingTask<Context>
    {
        public override void Run(Context context)
        {
            RepackOptions options = new RepackOptions
            {
                OutputFile = context.CliBinDir.CombineWithFilePath("VGAudioTools.exe").FullPath,
                InputAssemblies = new[]
                {
                    context.CliBinDir.CombineWithFilePath($"{context.LibBuilds["full"].ToolsFramework}/VGAudioTools.exe").FullPath,
                    context.CliBinDir.CombineWithFilePath($"{context.LibBuilds["full"].ToolsFramework}/VGAudio.dll").FullPath
                },
                SearchDirectories = new[] { "." }
            };
            new ILRepack(options).Repack();
        }

        public override bool ShouldRun(Context context) =>
            context.LibBuilds["full"].ToolsSuccess == true;

        public override void OnError(Exception exception, Context context) =>
            DisplayError(context, "Error creating merged assembly:\n" + exception.Message);
    }

    [Dependency(typeof(BuildUwp))]
    public sealed class PublishUwp : FrostingTask<Context>
    {
        public override void Run(Context context)
        {
            XDocument manifest = XDocument.Load(context.UwpDir.CombineWithFilePath("Package.appxmanifest").FullPath);
            XNamespace ns = manifest.Root?.GetDefaultNamespace();
            string packageVersion = manifest.Root?.Element(ns + "Identity")?.Attribute("Version")?.Value;

            string debugSuffix = context.IsReleaseBuild ? "" : "_Debug";
            string packageName = $"VGAudio_{packageVersion}_x86_x64_arm{debugSuffix}";
            DirectoryPath packageDir = context.UwpDir.Combine($"AppPackages/VGAudio_{packageVersion}{debugSuffix}_Test");

            FilePath appxbundle = packageDir.CombineWithFilePath($"{packageName}.appxbundle");
            var toCopy = new FilePathCollection(new[] { appxbundle }, PathComparer.Default);
            toCopy += packageDir.CombineWithFilePath($"{packageName}.cer");

            if (context.IsReleaseBuild)
            {
                toCopy += packageDir.CombineWithFilePath($"../{packageName}_bundle.appxupload");
            }

            context.EnsureDirectoryExists(context.UwpBinDir);
            context.CopyFiles(toCopy, context.UwpBinDir);
        }

        public override bool ShouldRun(Context context) =>
            context.OtherBuilds["uwp"] == true;

        public override void OnError(Exception exception, Context context) =>
            DisplayError(context, "Error publishing UWP app:\n" + exception.Message);
    }
}
