﻿using System;
using System.Collections.Generic;
using System.Linq;
using Cake.Common.IO;
using Cake.Core.IO;
using Cake.Frosting;
using static Build.Utilities;

namespace Build.Tasks
{
    [Dependency(typeof(PublishCli))]
    [Dependency(typeof(Test))]
    public sealed class SignCli : FrostingTask<Context>
    {
        public override void Run(Context context)
        {
            var possibleNames = new[] { "VGAudio.dll", "VGAudioCli.exe", "VGAudioCli.dll", "VGAudioTools.exe", "VGAudioCli.dll" };

            List<FilePath> toSign = context.LibBuilds.Values
                .SelectMany(build => possibleNames
                    .Select(file => context.CliBinDir
                        .Combine(build.CliFramework)
                        .CombineWithFilePath(file)))
                .ToList();

            //Add merged assembly
            toSign.Add(context.CliBinDir.CombineWithFilePath("VGAudioCli.exe"));
            toSign.Add(context.CliBinDir.CombineWithFilePath("VGAudioTools.exe"));

            SignFiles(context, toSign.Where(context.FileExists), context.ReleaseCertThumbprint);
        }

        public override bool ShouldRun(Context context) =>
            context.CliBuildsSucceeded &&
            context.TestsSucceeded &&
            CertificateExists(context.ReleaseCertThumbprint, true);

        public override void OnError(Exception exception, Context context) =>
            DisplayError(context, "Couldn't sign CLI assemblies:\n" + exception.Message);
    }

    [Dependency(typeof(PublishLibrary))]
    [Dependency(typeof(Test))]
    public sealed class SignLibrary : FrostingTask<Context>
    {
        public override void Run(Context context)
        {
            FilePathCollection packages = context.GetFiles($"{context.LibraryBinDir}/*.nupkg");

            foreach (FilePath file in packages)
            {
                DirectoryPath extracted = context.LibraryBinDir.Combine(file.GetFilenameWithoutExtension().ToString());
                context.Unzip(file, extracted);

                FilePathCollection toSign = context.GetFiles($"{extracted}/lib/**/VGAudio.dll");
                SignFiles(context, toSign, context.ReleaseCertThumbprint);
                context.Zip(extracted, context.LibraryBinDir.CombineWithFilePath(file.GetFilename()));
                context.DeleteDirectory(extracted, true);
            }
        }

        public override bool ShouldRun(Context context) =>
            context.LibraryBuildsSucceeded &&
            context.TestsSucceeded &&
            CertificateExists(context.ReleaseCertThumbprint, true);

        public override void OnError(Exception exception, Context context) =>
            DisplayError(context, "Couldn't sign nupkg:\n" + exception.Message);
    }

    [Dependency(typeof(PublishUwp))]
    [Dependency(typeof(Test))]
    public sealed class SignUwp : FrostingTask<Context>
    {
        public override void Run(Context context) =>
            SignFiles(context, context.GetFiles($"{context.UwpBinDir}/*.appxbundle"), context.ReleaseCertThumbprint);

        public override bool ShouldRun(Context context) =>
            context.OtherBuilds["uwp"] == true &&
            context.TestsSucceeded &&
            CertificateExists(context.ReleaseCertThumbprint, true);

        public override void OnError(Exception exception, Context context) =>
            DisplayError(context, "Couldn't sign UWP app:\n" + exception.Message);
    }
}
