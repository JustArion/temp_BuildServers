using System.IO;

namespace Extensions;

public static class DirectoryInfoEx
{
    extension(DirectoryInfo info)
    {
        public void CopyTo(DirectoryInfo target)
        {
            if (!info.Exists)
                throw new DirectoryNotFoundException($"Source directory does not exist or could not be found: {info.FullName}");
            
            target.Create();
            
            foreach (var file in info.GetFiles()) 
                file.CopyTo(Path.Combine(target.FullName, file.Name), overwrite: true);

            var dirs = info.GetDirectories();

            foreach (var dir in dirs) 
                dir.CopyTo(new DirectoryInfo(Path.Combine(target.FullName, dir.Name)));
        }
    }
}