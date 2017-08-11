﻿/*
 * SonarQube :: SCM :: TFVC :: Plugin
 * Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
 *
 * Licensed under the MIT License. See License.txt in the project root for license information.
 */

namespace SonarSource.TfsAnnotate
{
    using System;
    using System.IO;
    using System.Text;
    using System.Net;

    using Microsoft.TeamFoundation.VersionControl.Client;
    using Microsoft.VisualStudio.Services.Common;

    public class Program
    {
        private static readonly DateTime Epoch = new DateTime(1970, 1, 1);
        private static Uri serverUri = null;

        public static int Main(string[] args)
        {
            try
            {
                Console.InputEncoding = Encoding.UTF8;
                Console.OutputEncoding = Encoding.UTF8;

                if (args.Length != 0)
                {
                    Console.Error.WriteLine("This program is only expected to be called by the SonarQube TFS SCM plugin.");
                    return 1;
                }

                Console.WriteLine("Enter your credentials");
                Console.Out.Flush();

                var username = Console.ReadLine();
                var password = Console.ReadLine();
                var pat = Console.ReadLine();

                VssCredentials credentials;

                if (!String.IsNullOrEmpty(pat))
                {
                    credentials = new VssCredentials(new VssBasicCredential(new NetworkCredential(string.Empty, pat)));
                }
                else if (!String.IsNullOrEmpty(username) || !String.IsNullOrEmpty(password))
                {
                    credentials = new VssCredentials(new WindowsCredential(new NetworkCredential(username, password)));
                }
                else
                {
                    credentials = new VssCredentials(true);
                }
                credentials.PromptType = CredentialPromptType.DoNotPrompt;                    

                Console.WriteLine("Enter the Collection URI");
                Console.Out.Flush();
                var serverUriString = Console.ReadLine();

                if (!string.IsNullOrEmpty(serverUriString))
                {
                    if (!SetServerUri(serverUriString))
                    {
                        return 1;
                    }
                }

                using (var cache = new TfsCache(credentials))
                {
                    if (serverUri != null)
                    {
                        if (!UpdateCache(cache, serverUri))
                        {
                            return 1;
                        }
                    }

                    Console.Out.WriteLine("Enter the paths to annotate");
                    Console.Out.Flush();

                    string path;
                    while ((path = Console.ReadLine()) != null)
                    {
                        try
                        {
                            Console.Out.Flush();
                            Console.WriteLine(path);

                            if (!File.Exists(path))
                            {
                                FailOnFile("does not exist: " + path);
                                continue;
                            }

                            if (!Workstation.Current.IsMapped(path))
                            {
                                FailOnFile("is not in a mapped TFS workspace: " + path);
                                continue;
                            }

                            WorkspaceInfo workspaceInfo = Workstation.Current.GetLocalWorkspaceInfo(path);
                            WorkspaceVersionSpec version = new WorkspaceVersionSpec(workspaceInfo);

                            if (serverUri == null || workspaceInfo.ServerUri.AbsoluteUri != serverUri.AbsoluteUri)
                            {
                                serverUri = workspaceInfo.ServerUri;
                                if (!UpdateCache(cache, serverUri))
                                {
                                    return 1;
                                }
                            }

                            var versionControlServer = cache.GetVersionControlServer(serverUri);

                            IAnnotatedFile annotatedFile = new FileAnnotator(versionControlServer).Annotate(path, version);
                            if (annotatedFile == null)
                            {
                                FailOnFile("is not yet checked-in: " + path);
                                continue;
                            }

                            if (annotatedFile.IsBinary())
                            {
                                FailOnFile("is a binary one: " + path);
                                continue;
                            }

                            bool failed = false;
                            for (int i = 0; !failed && i < annotatedFile.Lines(); i++)
                            {
                                var state = annotatedFile.State(i);
                                if (state != AnnotationState.COMMITTED)
                                {
                                    FailOnFile($"line {(i + 1)} has not yet been checked-in ({state}): {path}");
                                    failed = true;
                                }
                            }
                            if (failed)
                            {
                                continue;
                            }

                            Console.WriteLine(annotatedFile.Lines());
                            for (int i = 0; i < annotatedFile.Lines(); i++)
                            {
                                Changeset changeset = annotatedFile.Changeset(i);
                                Console.Write(changeset.ChangesetId);
                                Console.Write('\t');
                                Console.Write(cache.GetEmailOrAccountName(serverUri, changeset.Owner));
                                Console.Write('\t');
                                Console.Write(ToUnixTimestampInMs(changeset.CreationDate));
                                Console.Write('\t');
                                Console.WriteLine(annotatedFile.Data(i));
                            }
                        }
                        catch (Exception e)
                        {
                            FailOnFile(e.Message);
                        }
                    }
                    Console.Out.Flush();
                }
                return 0;
            }
            catch(Exception e)
            {
                FailOnProject(e.Message);
                return 1;
            }
        }

        private static bool UpdateCache(TfsCache cache, Uri serverUri)
        {
            try
            {
                cache.EnsureAuthenticated(serverUri);
            }
            catch (Exception e)
            {
                FailOnProject("raised the following authentication exception: " + e.Message);
                return false;
            }

            VersionControlServer versionControlServer = cache.GetVersionControlServer(serverUri);
            Workstation.Current.EnsureUpdateWorkspaceInfoCache(versionControlServer, versionControlServer.AuthorizedUser);
            return true;
        }

        private static bool SetServerUri(String serverUriString)
        {
            try
            {
                serverUri = new Uri(serverUriString);
            }
            catch (UriFormatException e)
            {
                FailOnProject($"raised the following exception: {e.Message} Please enter the correct server URI.");
                return false;
            }
            return true;
        }

        private static long ToUnixTimestampInMs(DateTime dateTime)
        {
            var timespan = dateTime - Epoch;
            return Convert.ToInt64(timespan.TotalMilliseconds);
        }

        private static void FailOnFile(string reason)
        {
            Console.Out.WriteLine("AnnotationFailedOnFile");
            Console.Error.WriteLine("Unable to TFS annotate the following file which " + reason);
        }

        private static void FailOnProject(string reason)
        {
            Console.Out.WriteLine("AnnotationFailedOnProject");
            Console.Error.WriteLine("Unable to TFS annotate the project which " + reason);
        }
    }
}
