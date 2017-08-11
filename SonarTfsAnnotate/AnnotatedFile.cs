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

    internal sealed class AnnotatedFile : IAnnotatedFile
    {
        private const int UNKNOWN = -1;
        private const int LOCAL = 0;

        private readonly bool isBinary;
        private readonly string[] data;
        private readonly int lines;
        private readonly int[] revisions;
        private readonly int[] mappings;
        private readonly IDictionary<int, Changeset> changesets = new Dictionary<int, Changeset>();

        public AnnotatedFile(string path, int encoding)
        {
            if (encoding == -1)
            {
                isBinary = true;
            }
            else
            {
                data = File.ReadAllLines(path, Encoding.GetEncoding(encoding));
                lines = data.Length;
                revisions = new int[lines];
                mappings = new int[lines];
                for (int i = 0; i < lines; i++)
                {
                    revisions[i] = UNKNOWN;
                    mappings[i] = i;
                }
            }
        }

        public void Apply(Changeset changeset)
        {
            for (var i = 0; i < revisions.Length; i++)
            {
                if (revisions[i] == UNKNOWN)
                {
                    Associate(i, changeset);
                }
            }
        }

        public bool ApplyDiff(Changeset changeset, Dictionary<int, int> diff)
        {
            var done = true;

            for (var i = 0; i < revisions.Length; i++)
            {
                if (revisions[i] == UNKNOWN)
                {
                    int line = mappings[i];
                    if (!diff.ContainsKey(line))
                    {
                        Associate(i, changeset);
                    }
                    else
                    {
                        mappings[i] = diff[line];
                        done = false;
                    }
                }
            }

            return done;
        }

        private void Associate(int line, Changeset changeset)
        {
            int changesetId = changeset != null ? changeset.ChangesetId : LOCAL;
            revisions[line] = changesetId;
            if (!changesets.ContainsKey(changesetId))
            {
                changesets.Add(changesetId, changeset);
            }
        }

        public bool IsBinary()
        {
            return isBinary;
        }

        public int Lines()
        {
            ThrowIfBinaryFile();
            return lines;
        }

        public string Data(int line)
        {
            ThrowIfBinaryFile();
            return data[line];
        }

        public AnnotationState State(int line)
        {
            ThrowIfBinaryFile();
            switch (revisions[line])
            {
                case UNKNOWN:
                    return AnnotationState.UNKNOWN;
                case LOCAL:
                    return AnnotationState.LOCAL;
                default:
                    return AnnotationState.COMMITTED;
            }
        }

        public Changeset Changeset(int line)
        {
            ThrowIfBinaryFile();
            return changesets[revisions[line]];
        }

        private void ThrowIfBinaryFile()
        {
            if (IsBinary())
            {
                throw new InvalidOperationException("Not supported on binary files!");
            }
        }
    }
}