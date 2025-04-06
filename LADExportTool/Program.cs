using System;
using System.IO;
using System.Text.RegularExpressions;
using Siemens.Engineering;
using Siemens.Engineering.SW;

namespace LADExportTool
{

    internal class Program
    {
        string path;
        static void Main(string[] args)
        {
            string outputPath;
            string projectFilePath;
            (outputPath, projectFilePath) = checkParameters(args);
            if (projectFilePath == null || outputPath == null)
            {
                return;
            }
            TiaProject tiaProject =new TiaProject(projectFilePath,outputPath);
            Project project = tiaProject.project;
            
            if (project == null)
            {
                return;
            }

            foreach (PlcSoftware plcSoftware in tiaProject.EnumeratePlcSoftwares(project))
            {
                Console.WriteLine(plcSoftware.Name);
                Console.WriteLine("******************************************************");
                //List<PlcBlock> plcBlocks = tiaProject.EnumerateBlockGroups(plcSoftware);
                //foreach (PlcBlock plcBlock in plcBlocks)
                //{
                //    Console.WriteLine($"{plcBlock.Name}");
                //}
                tiaProject.ExportBlocksAsDocument(plcSoftware, tiaProject.ExportLADBlockAsDocument);
            }            

        }

        private static (string outputPath, string projectFilePath) checkParameters(string[] args)
        {
            string outputPath = null;
            string projectFilePath = null;

            if (args.Length == 0 || args[0].ToLower() == "-h" || args[0].ToLower() == "--help")
            {
                ShowHelp();
                return (outputPath, projectFilePath);
            }

            // 解析命令行参数
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i].ToLower())
                {
                    case "-o":
                        if (i + 1 < args.Length)
                        {
                            outputPath = args[i + 1];
                            i++; // 跳过下一个参数，因为它已经被处理
                        }
                        else
                        {
                            Console.WriteLine("错误: -o 参数需要一个路径值。");
                            return (outputPath, projectFilePath);
                        }
                        break;

                    case "-p":
                        if (i + 1 < args.Length)
                        {
                            projectFilePath = args[i + 1];
                            i++; // 跳过下一个参数，因为它已经被处理
                        }
                        else
                        {
                            Console.WriteLine("错误: -p 参数需要一个带路径的项目文件值。");
                            return (outputPath, projectFilePath);
                        }
                        break;

                    default:
                        Console.WriteLine($"未知参数: {args[i]}");
                        return (outputPath, projectFilePath);
                }
            }

            // 检查参数是否有效
            if (string.IsNullOrEmpty(outputPath))
            {
                Console.WriteLine("错误: 未指定输出路径 (-o)。");
                return (outputPath, projectFilePath);
            }

            if (string.IsNullOrEmpty(projectFilePath))
            {
                Console.WriteLine("错误: 未指定项目文件路径 (-p)。");
                return (outputPath, projectFilePath);
            }

            if (!IsValidProject(projectFilePath))
            {
                Console.WriteLine("错误: 错误的项目文件名 (-p)。");
                projectFilePath = null;
                return (outputPath, projectFilePath);
            }

            // 检查项目文件是否存在
            if (!File.Exists(projectFilePath))
            {
                Console.WriteLine($"错误: 项目文件不存在: {projectFilePath}");
                projectFilePath = null;
                return (outputPath, projectFilePath);
            }
            // 检查输出路径是否存在，如果不存在则创建
            if (!Directory.Exists(outputPath))
            {
                Directory.CreateDirectory(outputPath);
                Console.WriteLine($"输出路径已创建: {outputPath}");
            }

            return (outputPath, projectFilePath);
        }        

        private static void ShowHelp()
        {
            Console.WriteLine("使用说明:");
            Console.WriteLine("  LADExportTool.exe -o <输出路径> -p <项目文件路径>");
            Console.WriteLine();
            Console.WriteLine("参数:");
            Console.WriteLine("  -o <路径>          指定输出目录路径。如果目录不存在，将自动创建。");

            Console.WriteLine("  -p <路径>          指定带路径的项目文件。");
            Console.WriteLine("  -h, --help         显示帮助信息。");
            Console.WriteLine();
            Console.WriteLine("注意: 输出目录中后缀为 .s7dcl 和 .s7res 的所有文件都会被删除。");
            Console.WriteLine("示例:");
            Console.WriteLine("  LADExportTool.exe -o \"C:\\OutputFolder\" -p \"C:\\Path\\To\\ProjectFile.ap20\"");
            Console.WriteLine();


        }

        private static bool IsValidProject(string projectFilePath)
        {
            // 查找最后一个 '.' 的位置
            int lastDotIndex = projectFilePath.LastIndexOf('.');
            if (lastDotIndex == -1 || lastDotIndex == projectFilePath.Length - 1)
                return false;

            string pattern = @"\bap\d+\b";
            // 使用 Regex.IsMatch 方法检查字符串是否匹配模式
            return Regex.IsMatch(projectFilePath.Substring(lastDotIndex + 1), pattern);
        }
    }
}

