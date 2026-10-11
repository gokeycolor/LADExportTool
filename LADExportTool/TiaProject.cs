using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Siemens.Engineering.HW.Features;
using Siemens.Engineering.HW;
using Siemens.Engineering.SW.Blocks;
using Siemens.Engineering.SW;
using Siemens.Engineering;
using static LADExportTool.TiaProject;
using System.Security.Cryptography;

namespace LADExportTool
{
    // TIA Portal 项目封装类，用于连接项目、枚举 PLC、导出块文档等操作
    internal class TiaProject
    {
        // 当前已连接的 TIA Portal 项目对象
        public Project project;

        // 导出文件的根目录路径
        private readonly string outputPath;

        // 当前项目中找到的 PLC 软件列表
        private readonly List<PlcSoftware> plcSoftwares;

        // 构造函数：初始化项目连接和导出路径
        public TiaProject(string projectPath, string exportPath)
        {
            project = ConnectTiaProject(projectPath);
            outputPath = exportPath;
            plcSoftwares = new List<PlcSoftware> { };
        }

        // 枚举项目中所有设备（顶层设备 + 设备组中的设备）
        private void EnumerateDevicesInProject(Project project)
        {
            EnumerateDeviceObjects(project.Devices);
            EnumerateDevicesInGroups(project);
        }

        // 遍历项目中的设备组
        private void EnumerateDevicesInGroups(Project project)
        {
            foreach (DeviceUserGroup deviceUserGroup in project.DeviceGroups)
            {
                EnumerateDeviceUserGroup(deviceUserGroup);
            }
        }

        // 递归遍历设备组及其子组中的设备
        private void EnumerateDeviceUserGroup(DeviceUserGroup deviceUserGroup)
        {
            EnumerateDeviceObjects(deviceUserGroup.Devices);
            foreach (DeviceUserGroup subDeviceUserGroup in deviceUserGroup.Groups)
            {
                // 递归处理子设备组
                EnumerateDeviceUserGroup(subDeviceUserGroup);
            }
        }

        // 遍历设备组合中的设备项，筛选出 CPU 并获取其 PLC 软件
        private void EnumerateDeviceObjects(DeviceComposition deviceComposition)
        {
            foreach (Device device in deviceComposition)
            {
                foreach (DeviceItem deviceItem in device.DeviceItems)
                {
                    // 仅处理 CPU 类型的设备项
                    if (deviceItem.Classification.ToString() == "CPU")
                    {
                        plcSoftwares.Add(GetPlcSoftware(deviceItem));
                    }
                }
            }
        }

        // 对外提供：枚举项目中所有 PLC 软件对象
        public List<PlcSoftware> EnumeratePlcSoftwares(Project project)
        {
            EnumerateDevicesInProject(project);
            return plcSoftwares;
        }

        // 连接或打开目标 TIA Portal 项目
        private Project ConnectTiaProject(string projectPath)
        {
            try
            {
                Project project;

                // 如果已有 TIA Portal 进程正在运行，则优先尝试附加到已打开的项目
                if (TiaPortal.GetProcesses().Any())
                {
                    foreach (TiaPortalProcess tiaPortalProcess in TiaPortal.GetProcesses())
                    {
                        TiaPortalProcess process = TiaPortal.GetProcess(tiaPortalProcess.Id);
                        if (process.ProjectPath != null)
                        {
                            // 判断当前打开项目是否和目标路径一致
                            if (TiaPortal.GetProcess(tiaPortalProcess.Id).ProjectPath.FullName == projectPath)
                            {
                                project = TiaPortal.GetProcess(tiaPortalProcess.Id).Attach().Projects.First();
                                Console.WriteLine("project Name:  " + project.Name);
                                return project;
                            }
                        }
                    }
                }

                // 未找到已打开项目，则创建新的 TIA Portal 实例并打开指定项目
                TiaPortal tia = new TiaPortal(TiaPortalMode.WithoutUserInterface);
                project = tia.Projects.OpenWithUpgrade(new FileInfo(projectPath));

                Console.WriteLine("project Name:  " + project.Name);
                return project;
            }
            catch (Siemens.Engineering.MissingProductsException e)
            {
                Console.WriteLine(e.Message);
                return null;
            }
            catch (Exception)
            {
                throw;
            }
        }

        // 根据设备项解析出该设备对应的 PLC 软件实例
        private PlcSoftware GetPlcSoftware(DeviceItem deviceItem)
        {
            SoftwareContainer softwareContainer = deviceItem.GetService<SoftwareContainer>();
            if (softwareContainer != null)
            {
                Software softwareBase = softwareContainer.Software;
                PlcSoftware plcSoftware = softwareBase as PlcSoftware;
                return plcSoftware;
            }

            return null;
        }

        // 导出单个 LAD/F_LAD 编程语言块为文档
        public void ExportLADBlockAsDocument(PlcBlock block, string path)
        {
            try
            {
                // 仅导出 LAD 和 F_LAD 的程序块
                if (block.ProgrammingLanguage.ToString() == "LAD" || block.ProgrammingLanguage.ToString() == "F_LAD")
                {
                    // 如果块未启用知识产权保护，可以导出
                    if (!block.IsKnowHowProtected)
                    {
                        DocumentExportResult documentExportResult = block.ExportAsDocuments(new DirectoryInfo(path), block.Name);
                        DocumentResultState documentResultState = documentExportResult.State;
                        IEnumerable<FileInfo> exportedDocuments = documentExportResult.ExportedDocuments;
                        foreach (FileInfo exportedDocument in exportedDocuments)
                        {
                            Console.WriteLine(exportedDocument.Name);
                        }
                        Console.WriteLine(block.Name + " export " + documentResultState.ToString());
                    }
                    else
                        Console.WriteLine(block.Name + "启用了专有技术保护，无法导出。");
                    Console.WriteLine("------------------------------------------------");
                }
            }
            catch (Siemens.Engineering.EngineeringNotSupportedException e)
            {
                Console.WriteLine(e.Message);
            }
            catch (Exception)
            {
                throw;
            }
        }

        // 定义导出单个块的委托签名
        public delegate void ExportBlockAsDocument(PlcBlock block, string path);

        // 导出某个 PLC 软件中的所有块文档
        public void ExportBlocksAsDocument(PlcSoftware plcSoftware, ExportBlockAsDocument exportBlockAsDocument)
        {
            // 根据 PLC 名称生成对应输出目录
            string path = GetPath(outputPath, plcSoftware.Name);

            // 遍历普通块组
            EnumerateBlockGroups(plcSoftware, path, exportBlockAsDocument);

            // 遍历系统块组
            EnumerateSystemgroupForSystemblocks(plcSoftware, path, exportBlockAsDocument);
        }

        // 检索系统生成的系统块组
        private void EnumerateSystemgroupForSystemblocks(PlcSoftware plcSoftware, string path, ExportBlockAsDocument exportBlockAsDocument)
        {
            plcSoftware.BlockGroup.SystemBlockGroups.ForEach((systemBlockGroup) =>
            {
                EnumerateSystemBlockGroups(systemBlockGroup, path, exportBlockAsDocument);
            });
        }

        // 递归遍历系统块组及其子组中的所有块
        private void EnumerateSystemBlockGroups(PlcSystemBlockGroup systemBlockGroup, string path, ExportBlockAsDocument exportBlockAsDocument)
        {
            systemBlockGroup.Blocks.ForEach(block => {
                exportBlockAsDocument(block, path);
            });
            foreach (PlcSystemBlockGroup group in systemBlockGroup.Groups)
            {
                EnumerateSystemBlockGroups(group, path, exportBlockAsDocument);
            }
        }

        // 枚举所有块用户组，包括子组中的块
        private void EnumerateBlockGroups(PlcSoftware plcSoftware, string path, ExportBlockAsDocument exportBlockAsDocument)
        {
            plcSoftware.BlockGroup.Blocks.ForEach((block) =>
            {
                exportBlockAsDocument(block, path);
            });
            foreach (PlcBlockUserGroup blockUserGroup in plcSoftware.BlockGroup.Groups)
            {
                EnumerateBlockUserGroups(blockUserGroup, path, exportBlockAsDocument);
            }
        }

        // 递归遍历块用户组及其子组中的块
        private void EnumerateBlockUserGroups(PlcBlockUserGroup blockUserGroup, string path, ExportBlockAsDocument exportBlockAsDocument)
        {
            blockUserGroup.Blocks.ForEach(block =>
            {
                exportBlockAsDocument(block, path);
            });
            foreach (PlcBlockUserGroup subBlockUserGroup in blockUserGroup.Groups)
            {
                EnumerateBlockUserGroups(subBlockUserGroup, path, exportBlockAsDocument);
                // 递归处理子组
            }
        }

        // 返回某个 PLC 中所有块的列表
        public List<PlcBlock> EnumerateBlockGroups(PlcSoftware plcSoftware)
        {
            List<PlcBlock> blocks = new List<PlcBlock>();
            blocks.AddRange(plcSoftware.BlockGroup.Blocks);

            foreach (PlcBlockUserGroup blockUserGroup in plcSoftware.BlockGroup.Groups)
            {
                blocks.AddRange(EnumerateBlockUserGroups(blockUserGroup));
            }

            return blocks;
        }

        // 递归获取某个块组中的所有块
        private List<PlcBlock> EnumerateBlockUserGroups(PlcBlockUserGroup blockUserGroup)
        {
            List<PlcBlock> blocks = new List<PlcBlock>();

            // 添加当前组的块
            blocks.AddRange(blockUserGroup.Blocks);

            // 递归处理子组
            foreach (PlcBlockUserGroup subBlockUserGroup in blockUserGroup.Groups)
            {
                blocks.AddRange(EnumerateBlockUserGroups(subBlockUserGroup));
            }

            return blocks;
        }

        // 根据 PLC 名称生成导出目录，若目录不存在则创建，若存在则清理旧导出文件
        private string GetPath(string path, string plcName)
        {
            string directoryPath = path + "\\" + plcName;

            // 检查目录是否存在
            if (!Directory.Exists(directoryPath))
            {
                // 如果目录不存在，则创建目录
                Directory.CreateDirectory(directoryPath);
                Console.WriteLine($"目录已创建: {directoryPath}");
            }
            else
            {
                Console.WriteLine($"目录已存在: {directoryPath}");

                // 清空目录中后缀为 .s7dcl 和 .s7res 的所有文件
                string[] fileExtensions = { "*.s7dcl", "*.s7res" };

                foreach (string extension in fileExtensions)
                {
                    string[] files = Directory.GetFiles(directoryPath, extension);

                    foreach (string file in files)
                    {
                        File.Delete(file);
                        Console.WriteLine($"已删除文件: {file}");
                    }
                }
            }
            return directoryPath;
        }
    }
}
