﻿using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reflection;
using System.Threading;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Framework;
using TestMyCode.CSharp.Core.Extensions;
using Xunit.Runners;

namespace TestMyCode.CSharp.Core.Compiler
{
    public class ProjectCompiler
    {
        private const string BIN_PATH = "bin";
        private const string OUTPUT_PATH = "output";

        private readonly ProjectCollection ProjectCollection;

        public ProjectCompiler()
        {
            this.ProjectCollection = new ProjectCollection();
            this.ProjectCollection.SetGlobalProperty("PublishDir", Path.Combine(ProjectCompiler.BIN_PATH, ProjectCompiler.OUTPUT_PATH));
        }

        public ICollection<string> CompileTestProjects(string projectPath)
        {
            List<string> files = new List<string>();

            foreach (string projectFile in Directory.EnumerateFiles(projectPath, "*Tests.csproj", SearchOption.AllDirectories))
            {
                Project project = this.ProjectCollection.LoadProject(projectFile);

                string projectRoot = Path.GetDirectoryName(projectFile) ?? string.Empty;

                this.CleanOutput(projectRoot);

                if (!this.CompileTestProject(project, out IReadOnlyList<string>? compilationErrors))
                {
                    throw new CompilationFaultedException(compilationErrors ?? Array.Empty<string>());
                }
                
                string assemblyName = project.GetPropertyValue("AssemblyName");

                string assemblyPath = Path.Combine(projectRoot, ProjectCompiler.BIN_PATH, ProjectCompiler.OUTPUT_PATH, $"{assemblyName}.dll");

                files.Add(assemblyPath);
            }

            return files;
        }

        private void CleanOutput(string projectRoot)
        {
            string bin = Path.Combine(projectRoot, "bin");
            if (Directory.Exists(bin))
            {
                DirectoryInfo dir = new DirectoryInfo(bin);
                dir.MoveTo(Path.Combine(projectRoot, "bin_OLD")); //Workaround as Delete is non blocking
                dir.Delete(recursive: true);
            }

            string obj = Path.Combine(projectRoot, "obj");
            if (Directory.Exists(obj))
            {
                DirectoryInfo dir = new DirectoryInfo(obj);
                dir.MoveTo(Path.Combine(projectRoot, "obj_OLD")); //Workaround as Delete is non blocking
                dir.Delete(recursive: true);
            }
        }

        private bool CompileTestProject(Project project, out IReadOnlyList<string>? compilationErrors)
        {
            CompilerOutputLogger logger = new CompilerOutputLogger();

            bool build = project.Build(targets: new[]
            {
                "Clean",
                "Restore",
                "Publish"
            }, loggers: new ILogger[]
            {
                logger
            });

            compilationErrors = logger.CompileErrors;

            return build;
        }

        private class CompilerOutputLogger : ILogger
        {
            private List<string>? _CompileErrors;

            public void Initialize(IEventSource eventSource)
            {
                eventSource.ErrorRaised += (sender, args) =>
                {
                    this.AddCompileError(args);
                };
            }

            private void AddCompileError(BuildErrorEventArgs args)
            {
                this._CompileErrors ??= new List<string>();
                this._CompileErrors.Add($"Error {args.Code} - {args.Message} in file {args.File} on line {args.LineNumber}. {args.GetNoobFriendlyTip()}");
            }

            public void Shutdown()
            {
            }

            internal IReadOnlyList<string>? CompileErrors => this._CompileErrors;

            string ILogger.Parameters { get; set; } = string.Empty;

            LoggerVerbosity ILogger.Verbosity
            {
                get => LoggerVerbosity.Diagnostic;
                set { } //Don't allow set
            }
        }
    }
}
