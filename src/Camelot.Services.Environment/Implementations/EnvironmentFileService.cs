using System.IO;
using System.Threading.Tasks;
using Camelot.Services.Environment.Interfaces;

namespace Camelot.Services.Environment.Implementations
{
    public class EnvironmentFileService : IEnvironmentFileService
    {
        public FileInfo GetFile(string file) =>
            new FileInfo(file);

        public string[] GetFiles(string directory) =>
            Directory.GetFiles(directory);

        public bool CheckIfExists(string filePath) =>
            Directory.Exists(filePath);

        public void Copy(string source, string destination, bool overwrite) =>
            File.Copy(source, destination, overwrite);

        public void Move(string oldFilePath, string newFilePath) =>
            File.Move(oldFilePath, newFilePath);

        public void Delete(string filePath) =>
            File.Delete(filePath);

        public Task WriteTextAsync(string filePath, string text) =>
            File.WriteAllTextAsync(filePath, text);

        public Task WriteBytesAsync(string filePath, byte[] bytes) =>
            File.WriteAllBytesAsync(filePath, bytes);
    }
}