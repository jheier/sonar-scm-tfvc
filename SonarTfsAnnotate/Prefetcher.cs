/*
 * SonarQube :: SCM :: TFVC :: Plugin
 * Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
 *
 * Licensed under the MIT License. See License.txt in the project root for license information.
 */
using System;
using System.Threading;

using Microsoft.TeamFoundation.VersionControl.Client;

namespace SonarSource.TfsAnnotate
{
    sealed class Prefetcher
    {
        private readonly Item item;
        private readonly string filename;
        private readonly ManualResetEvent manualResetEvent;

        public Prefetcher(Item item, string filename, ManualResetEvent manualResetEvent)
        {
            this.item = item;
            this.filename = filename;
            this.manualResetEvent = manualResetEvent;
        }

        public void Prefetch(object o)
        {
            try
            {
                item.DownloadFile(filename);
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e.Message);
            }
            finally
            {
                manualResetEvent.Set();
            }
        }
    }
}