using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Gehtsoft.EF.Serialization.IO
{
    public interface IBlobAccessor
    {
        string Save(byte[] blob);
        byte[] Load(string s);
    }

    public class Base64BlobAccessor : IBlobAccessor
    {
        public string Save(byte[] blob) => TextFormatter.Format(blob);

        public byte[] Load(string s) => TextFormatter.ParseByteArray(s);
    }

    public class FileBlobAccessor : IBlobAccessor
    {
        private readonly DirectoryInfo mDirectory;

        public FileBlobAccessor(string directory)
        {
            mDirectory = new DirectoryInfo(Path.GetFullPath(directory));
            if (!mDirectory.Exists)
                Directory.CreateDirectory(mDirectory.FullName);
        }

        public string Save(byte[] blob)
        {
            FileInfo fi;
            do
            {
                fi = new FileInfo(Path.Combine(mDirectory.FullName, Guid.NewGuid().ToString("N")));
            } while (fi.Exists);

            using (FileStream fs = new FileStream(fi.FullName, FileMode.Create, FileAccess.Write, FileShare.None))
                fs.Write(blob, 0, blob.Length);
            return fi.Name;
        }

        public byte[] Load(string s)
        {
            using (FileStream fs = new FileStream(Path.Combine(mDirectory.FullName, s), FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                int l = (int)fs.Length;
                byte[] b = new byte[l];
                fs.Read(b, 0, l);
                return b;
            }
        }
    }
}
