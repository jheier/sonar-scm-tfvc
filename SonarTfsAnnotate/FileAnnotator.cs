/*
 * SonarQube :: SCM :: TFVC :: Plugin
 * Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
 *
 * Licensed under the MIT License. See License.txt in the project root for license information.
 */

namespace SonarSource.TfsAnnotate
{    
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;

    using Microsoft.TeamFoundation.VersionControl.Client;
    using Microsoft.TeamFoundation.VersionControl.Common;

    internal class FileAnnotator
    {
        private readonly VersionControlServer server;

        public FileAnnotator(VersionControlServer server)
        {
            this.server = server;
        }

        public IAnnotatedFile Annotate(string path, VersionSpec version)
        {
            var options = new DiffOptions
            {
                Flags = DiffOptionFlags.EnablePreambleHandling | DiffOptionFlags.IgnoreLeadingAndTrailingWhiteSpace | DiffOptionFlags.IgnoreEndOfLineDifference
            };

            PendingChange[] pendingChanges = server.GetWorkspace(path).GetPendingChanges(path);
            if (pendingChanges.Length >= 2)
            {
                throw new InvalidOperationException("Expected at most 1 pending change, but got " + pendingChanges.Length);
            }

            Changeset currentChangeset = null;

            AnnotatedFile annotatedFile;
            string currentPath;
            int currentEncoding;

            if (pendingChanges.Length == 1 && (pendingChanges[0].ChangeType & ChangeType.Edit) != 0)
            {
                annotatedFile = new AnnotatedFile(path, pendingChanges[0].Encoding);
                if (annotatedFile.IsBinary())
                {
                    return annotatedFile;
                }
                currentPath = path;
                currentEncoding = pendingChanges[0].Encoding;
            }
            else
            {
                annotatedFile = null;
                currentPath = null;
                currentEncoding = 0;
            }

            using (var historyProvider = new HistoryProvider(server, path, version))
            {
                var done = false;

                while (!done && historyProvider.Next())
                {
                    Changeset previousChangeset = historyProvider.Changeset();

                    string previousPath = historyProvider.Filename();
                    int previousEncoding = previousChangeset.Changes[0].Item.Encoding;

                    if (annotatedFile == null)
                    {
                        annotatedFile = new AnnotatedFile(previousPath, previousEncoding);
                        if (annotatedFile.IsBinary())
                        {
                            return annotatedFile;
                        }
                    }
                    else if (previousEncoding == -1)
                    {
                        annotatedFile.Apply(currentChangeset);
                        done = true;
                    }
                    else
                    {
                        var diff = Diff(Difference.DiffFiles(currentPath, currentEncoding, previousPath, previousEncoding, options));
                        done = annotatedFile.ApplyDiff(currentChangeset, diff);
                    }

                    currentChangeset = previousChangeset;
                    currentEncoding = previousEncoding;
                    currentPath = previousPath;
                }

                if (annotatedFile != null)
                {
                    annotatedFile.Apply(currentChangeset);
                }
            }

            return annotatedFile;
        }

        private static Dictionary<int, int> Diff(DiffSegment diffSegment)
        {
            var result = new Dictionary<int, int>();

            while (diffSegment != null)
            {
                int originalLine = diffSegment.OriginalStart;
                int modifiedLine = diffSegment.ModifiedStart;
                for (var i = 0; i < diffSegment.OriginalLength; i++)
                {
                    result.Add(originalLine, modifiedLine);
                    originalLine++;
                    modifiedLine++;
                }

                diffSegment = diffSegment.Next;
            }

            return result;
        }
    }
}