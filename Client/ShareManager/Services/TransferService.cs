using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;

namespace ShareManager.App.Services
{
    public class TransferService
    {
        public async Task ProcessTransferAsync(string[] sourcePaths, string smbBasePath, string uuid, string originalName)
        {
            string tempDir = Path.Combine(smbBasePath, ".temp");
            Directory.CreateDirectory(tempDir);

            string destinationPath = Path.Combine(tempDir, uuid);

            await Task.Run(() =>
            {
                if (sourcePaths.Length == 1 && File.Exists(sourcePaths[0]))
                {
                    File.Copy(sourcePaths[0], destinationPath, true);
                }
                else
                {
                    using (FileStream zipToOpen = new FileStream(destinationPath, FileMode.Create))
                    {
                        using (ZipArchive archive = new ZipArchive(zipToOpen, ZipArchiveMode.Create))
                        {
                            foreach (string path in sourcePaths)
                            {
                                if (File.Exists(path))
                                {
                                    archive.CreateEntryFromFile(path, Path.GetFileName(path));
                                }
                                else if (Directory.Exists(path))
                                {
                                    AddDirectoryToArchive(archive, path, Path.GetFileName(path));
                                }
                            }
                        }
                    }
                }
            });
        }

        private void AddDirectoryToArchive(ZipArchive archive, string sourceDir, string entryNamePrefix)
        {
            foreach (string file in Directory.GetFiles(sourceDir))
            {
                archive.CreateEntryFromFile(file, Path.Combine(entryNamePrefix, Path.GetFileName(file)));
            }

            foreach (string subDir in Directory.GetDirectories(sourceDir))
            {
                AddDirectoryToArchive(archive, subDir, Path.Combine(entryNamePrefix, Path.GetFileName(subDir)));
            }
        }
    }
}
