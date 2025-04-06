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
    internal class TiaProject
    {

        public Project project;
        private readonly string outputPath;
        private readonly List<PlcSoftware> plcSoftwares;


        public TiaProject(string projectPath, string exportPath)
        {
            project = ConnectTiaProject(projectPath);
            outputPath = exportPath;
            plcSoftwares = new List<PlcSoftware> { };
        }

        //Enamerate devices in groups or sub-groups
        private void EnumerateDevicesInProject(Project project)
        {
            EnumerateDeviceObjects(project.Devices);
            EnumerateDevicesInGroups(project);
        }
        private void EnumerateDevicesInGroups(Project project)
        {
            foreach (DeviceUserGroup deviceUserGroup in project.DeviceGroups)
            {
                EnumerateDeviceUserGroup(deviceUserGroup);
            }
        }
        private void EnumerateDeviceUserGroup(DeviceUserGroup deviceUserGroup)
        {
            EnumerateDeviceObjects(deviceUserGroup.Devices);
            foreach (DeviceUserGroup subDeviceUserGroup in deviceUserGroup.Groups)
            {
                // recursion 
                EnumerateDeviceUserGroup(subDeviceUserGroup);
            }
        }
        private void EnumerateDeviceObjects(DeviceComposition deviceComposition)
        {
            foreach (Device device in deviceComposition)
            {
                foreach (DeviceItem deviceItem in device.DeviceItems)
                {
                    if (deviceItem.Classification.ToString() == "CPU")
                    {
                        plcSoftwares.Add(GetPlcSoftware(deviceItem));
                    }
                }
            }
        }

        public List<PlcSoftware> EnumeratePlcSoftwares(Project project)
        {
            EnumerateDevicesInProject(project);
            return plcSoftwares;
        }

        private Project ConnectTiaProject(string projectPath)
        {
            try
            {
                Project project;
                if (TiaPortal.GetProcesses().Any())
                {
                    foreach (TiaPortalProcess tiaPortalProcess in TiaPortal.GetProcesses())
                    {
                        TiaPortalProcess process = TiaPortal.GetProcess(tiaPortalProcess.Id);
                        if (process.ProjectPath != null)
                        {
                            //Console.WriteLine("tia portal id:  " + tiaPortalProcess.Id + "project Path: " + TiaPortal.GetProcess(tiaPortalProcess.Id).ProjectPath.FullName);
                            if (TiaPortal.GetProcess(tiaPortalProcess.Id).ProjectPath.FullName == projectPath)
                            {
                                project = TiaPortal.GetProcess(tiaPortalProcess.Id).Attach().Projects.First();
                                //Console.WriteLine("Attach process id:  " + tiaPortalProcess.Id);
                                Console.WriteLine("project Name:  " + project.Name);
                                return project;
                            }
                        }

                    }
                }
                TiaPortal tia = new TiaPortal(TiaPortalMode.WithoutUserInterface);
                project = tia.Projects.OpenWithUpgrade(new FileInfo(projectPath));

                //Console.WriteLine("Open Tia process id:  " + TiaPortal.GetProcesses().First());
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


        public void ExportLADBlockAsDocument(PlcBlock block, string path)
        {
            try
            {
                if (block.ProgrammingLanguage.ToString() == "LAD" || block.ProgrammingLanguage.ToString() == "F_LAD")
                {
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

        public delegate void ExportBlockAsDocument(PlcBlock block, string path);
        public void ExportBlocksAsDocument(PlcSoftware plcSoftware, ExportBlockAsDocument exportBlockAsDocument)
        {
            // 遍历程序块文件夹
            string path = GetPath(outputPath, plcSoftware.Name);
            //Console.WriteLine("------------------------------------------");
            EnumerateBlockGroups(plcSoftware, path, exportBlockAsDocument);
            //Console.WriteLine("------------------------------------------");
            //Console.WriteLine("system block");
            EnumerateSystemgroupForSystemblocks(plcSoftware, path, exportBlockAsDocument);



        }
        //Retrieves the system generated group for system blocks
        private void EnumerateSystemgroupForSystemblocks(PlcSoftware plcSoftware, string path, ExportBlockAsDocument exportBlockAsDocument)
        {
            plcSoftware.BlockGroup.SystemBlockGroups.ForEach((systemBlockGroup) =>
            {
                EnumerateSystemBlockGroups(systemBlockGroup, path, exportBlockAsDocument);
            });
        }
        private void EnumerateSystemBlockGroups(PlcSystemBlockGroup systemBlockGroup, string path, ExportBlockAsDocument exportBlockAsDocument)
        {
            systemBlockGroup.Blocks.ForEach(block => {
                //Console.WriteLine(block.Name);
                exportBlockAsDocument(block, path);
            });
            foreach (PlcSystemBlockGroup group in systemBlockGroup.Groups)
            {
                EnumerateSystemBlockGroups(group, path, exportBlockAsDocument);
            }
        }

        //Enumerates all block user groups including sub groups
        private void EnumerateBlockGroups(PlcSoftware plcSoftware, string path, ExportBlockAsDocument exportBlockAsDocument)
        {
            plcSoftware.BlockGroup.Blocks.ForEach((block) =>
            {
                exportBlockAsDocument(block,path);  
            });
            foreach (PlcBlockUserGroup blockUserGroup in plcSoftware.BlockGroup.Groups)
            {

                EnumerateBlockUserGroups(blockUserGroup, path, exportBlockAsDocument);
            }
        }
        private void EnumerateBlockUserGroups(PlcBlockUserGroup blockUserGroup, string path, ExportBlockAsDocument exportBlockAsDocument)
                {
                    blockUserGroup.Blocks.ForEach(block =>
                    {
                        //Console.WriteLine(block.Name);
                        exportBlockAsDocument(block, path);
                    });
                    foreach (PlcBlockUserGroup subBlockUserGroup in blockUserGroup.Groups)
                    {

                        EnumerateBlockUserGroups(subBlockUserGroup, path, exportBlockAsDocument);
                        // recursion
                    }
                }
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
