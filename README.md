# LADExportTool
 导出TIA V20 项目中所有LAD 块

# 使用说明

```bash
LADExportTool.exe -o <输出路径> -p <项目文件路径>
```

## 参数

- `-o <路径>`：指定输出目录路径。如果目录不存在，将自动创建。
- `-p <路径>`：指定带路径的项目文件。
- `-h, --help`：显示帮助信息。

## 注意

输出目录中后缀为 `.s7dcl` 和 `.s7res` 的所有文件都会被删除。

## 示例

```bash
LADExportTool.exe -o "C:\OutputFolder" -p "C:\Path\To\ProjectFile.ap20"
```

