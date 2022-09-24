using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;

//
//  BSD 3-Clause License
//
//  Copyright (c) 2020, Pharap (@Pharap)
//  All rights reserved.
//
//  Redistribution and use in source and binary forms, with or without
//  modification, are permitted provided that the following conditions are met:
//
//  1. Redistributions of source code must retain the above copyright notice, this
//     list of conditions and the following disclaimer.
//
//  2. Redistributions in binary form must reproduce the above copyright notice,
//     this list of conditions and the following disclaimer in the documentation
//     and/or other materials provided with the distribution.
//
//  3. Neither the name of the copyright holder nor the names of its
//     contributors may be used to endorse or promote products derived from
//     this software without specific prior written permission.
//
//  THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
//  AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
//  IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
//  DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE
//  FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL
//  DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR
//  SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER
//  CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY,
//  OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
//  OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
//

namespace PlaylistGenerator
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("Usage: <path> ...");
                return;
            }

            var files = GetFiles(args);

            if(files.Length > 0)
                Process(files);

            var directories = GetDirectories(args);

            if (directories.Length > 0)
                Process(directories);
        }

        static FileInfo[] GetFiles(IEnumerable<string> args)
        {
            return args.Select(path => new FileInfo(path)).Where(info => info.Exists).ToArray();
        }

        static void Process(IEnumerable<FileInfo> files)
        {
            Process(new DirectoryInfo(Environment.CurrentDirectory), files);
        }

        static DirectoryInfo[] GetDirectories(IEnumerable<string> args)
        {
            return args.Select(path => new DirectoryInfo(path)).Where(info => info.Exists).ToArray();
        }

        static void Process(IEnumerable<DirectoryInfo> directories)
        {
            foreach (var directory in directories)
                Process(directory);
        }

        static void Process(DirectoryInfo directory)
        {
            Process(directory, EnumerateAllFiles(directory));
        }

        static IEnumerable<FileInfo> EnumerateAllFiles(DirectoryInfo rootDirectory)
        {
            var stack = new Queue<DirectoryInfo>();

            stack.Enqueue(rootDirectory);

            while (stack.Count > 0)
            {
                var currentDirectory = stack.Dequeue();

                var directories = currentDirectory
                    .EnumerateDirectories()
                    .OrderBy(info => info.Name)
                    .ThenBy(info => info.Name);

                foreach (var directory in directories)
                    stack.Enqueue(directory);

                var files = currentDirectory
                    .EnumerateFiles()
                    .Where(info => acceptedExtensions.Contains(Path.GetExtension(info.FullName)));

                foreach (var file in files)
                    yield return file;
            }
        }

        static readonly HashSet<string> acceptedExtensions = new HashSet<string>
        {
            ".mp4", ".mkv", ".webm"
        };

        static void Process(DirectoryInfo directory, IEnumerable<FileInfo> files)
        {
            using (var writer = CreateWriter(directory))
            {
                writer.WriteStartDocument();
                {
                    writer.WriteStartElement("playlist", "http:" + "//xspf.org/ns/0/");
                    writer.WriteAttributeString("version", "1");

                    writer.WriteElementString("title", directory.Name);
                    {
                        writer.WriteStartElement("trackList");
                        {
                            WriteTrackList(writer, files);
                        }
                        writer.WriteEndElement();
                    }
                }
                writer.WriteEndDocument();
            }
        }

        static XmlWriter CreateWriter(DirectoryInfo directory)
        {
            var writerPath = Path.Combine(directory.Parent.FullName, directory.Name + ".xspf");

            var settings = new XmlWriterSettings()
            {
                Indent = true,
                IndentChars = "\t"
            };

            return XmlTextWriter.Create(writerPath, settings);
        }

        static void WriteTrackList(XmlWriter writer, IEnumerable<FileInfo> files, int trackStart = 1)
        {
            foreach (var pair in files.Zip(Enumerable.Range(trackStart, int.MaxValue), Tuple.Create))
            {
                var video = pair.Item1;
                var trackNum = pair.Item2;

                writer.WriteStartElement("track");

                writer.WriteElementString("trackNum", trackNum.ToString());
                writer.WriteElementString("title", Path.GetFileNameWithoutExtension(video.Name));
                writer.WriteElementString("location", MakeRelativeUri(video));

                var fileName = Path.GetFileNameWithoutExtension(video.Name);

                var relatives = video.Directory
                    .EnumerateFiles(fileName + ".*")
                    .ToDictionary(info => Path.GetExtension(info.FullName));

                {
                    FileInfo descriptionFile;
                    if (relatives.TryGetValue(".description", out descriptionFile))
                        writer.WriteElementString("annotation", File.ReadAllText(descriptionFile.FullName));
                }

                {
                    FileInfo imageFile;
                    if (relatives.TryGetValue(".jpg", out imageFile))
                        writer.WriteElementString("image", MakeRelativeUri(imageFile));
                }

                writer.WriteEndElement();
            }
        }

        static string MakeRelativeUri(FileInfo info)
        {
            return (Uri.EscapeUriString(info.Directory.Name) + "/" + Uri.EscapeUriString(info.Name));
        }
    }
}
