﻿using CommandLine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace MusicDecrypto
{
    internal static class Program
    {
        static void Main(string[] args)
        {
            string[] inputPaths = null;

            CommandLine.Parser.Default.ParseArguments<Options>(args)
                .WithParsed<Options>(opts =>
                {
                    if (opts.OutputDir != null)
                    {
                        if (Directory.Exists(opts.OutputDir)) Decrypto.OutputDir = opts.OutputDir;
                        else Console.WriteLine($"[WARN] Specified output directory {opts.OutputDir} does not exist.");
                    }
                    Decrypto.SkipDuplicate = opts.SkipDuplicate;
                    TencentDecrypto.ForceRename = opts.ForceRename;
                    inputPaths = opts.InputPaths.ToArray();
                })
                .WithNotParsed<Options>(errs => { });

            if (inputPaths != null)
            {
                List<string> foundPaths = new List<string>();

                foreach (string path in inputPaths)
                {
                    try
                    {
                        if (Directory.Exists(path))
                        {
                            foundPaths.AddRange(
                                Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories)
                                    .Where(file =>
                                        file.ToLower().EndsWith(".ncm") ||
                                        file.ToLower().EndsWith(".mflac") ||
                                        file.ToLower().EndsWith(".qmc0") ||
                                        file.ToLower().EndsWith(".qmc3") ||
                                        file.ToLower().EndsWith(".qmcogg") ||
                                        file.ToLower().EndsWith(".qmcflac"))
                            );
                        }
                        else if (File.Exists(path) && (
                            path.ToLower().EndsWith(".ncm") ||
                            path.ToLower().EndsWith(".mflac") ||
                            path.ToLower().EndsWith(".qmc0") ||
                            path.ToLower().EndsWith(".qmc3") ||
                            path.ToLower().EndsWith(".qmcogg") ||
                            path.ToLower().EndsWith(".qmcflac")))
                        {
                            foundPaths.Add(path);
                        }
                    }
                    catch (IOException e)
                    {
                        Console.WriteLine(e.ToString());
                    }
                }

                string[] trimmedPaths = foundPaths.Where((x, i) => foundPaths.FindIndex(y => y == x) == i).ToArray();

                if (trimmedPaths.Length > 0)
                {
                    _ = Parallel.ForEach(trimmedPaths, file =>
                    {
                        Decrypto decrypto = null;

                        try
                        {
                            switch (Path.GetExtension(file))
                            {
                                case ".ncm":
                                    decrypto = new NetEaseDecrypto(file);
                                    break;
                                case ".qmc0":
                                case ".qmc3":
                                    decrypto = new TencentFixedDecrypto(file, "audio/mpeg");
                                    break;
                                case ".qmcogg":
                                    decrypto = new TencentFixedDecrypto(file, "audio/ogg");
                                    break;
                                case ".qmcflac":
                                    decrypto = new TencentFixedDecrypto(file, "audio/flac");
                                    break;
                                case ".mflac":
                                    decrypto = new TencentDynamicDecrypto(file, "audio/flac");
                                    break;
                                default:
                                    Console.WriteLine($"[WARN] Cannot recognize {file}");
                                    break;
                            }

                            if (decrypto != null) decrypto.Dump();
                        }
                        catch (IOException e)
                        {
                            Console.WriteLine(e.ToString());
                        }
                        finally
                        {
                            if (decrypto != null) decrypto.Dispose();
                        }
                    });

                    Console.WriteLine($"Program finished with {trimmedPaths.Length} files requested and {Decrypto.SuccessCount} files saved successfully.");
                    return;
                }

                Console.WriteLine("[WARN] Found no valid file from specified path(s).");
            }
        }

        internal class Options
        {
            [Option('d', "skip-duplicate", Required = false, HelpText = "Do not overwrite existing files.")]
            public bool SkipDuplicate { get; set; } = false;

            [Option('n', "force-rename", Required = false, HelpText = "Try to fix Tencent file name basing on metadata.")]
            public bool ForceRename { get; set; } = false;

            [Option('o', "output", Required = false, HelpText = "Specify output directory for all files.")]
            public string OutputDir { get; set; }

            [Value(0, Required = true, MetaName = "Path", HelpText = "Specify the input files and/or directories.")]
            public IEnumerable<string> InputPaths { get; set; }
        }
    }
}
