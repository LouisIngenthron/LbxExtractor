using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LbxDecoder
{
    public class Program
    {
        private const bool ReverseEndianness = false;

        public static void Main(string[] args)
        {
            Console.WriteLine("MOO2 LBX Extractor v1.0");
            Console.WriteLine("Extracts an LBX archive into individual files.");
            Console.WriteLine("Author: Louis Ingenthron");
            Console.WriteLine("Last modified: 8/27/2017");
            Console.WriteLine();

            if (args.Length <= 0)
            {
                Console.WriteLine("Usage: LbxExtractor.exe \"Filename.lbx\"");
                Console.WriteLine("Or just drag and drop an LBX file on to LbxExtractor.exe");
                Console.WriteLine("Note: Additional files can be provided as additional arguments");
                Console.WriteLine();
            }
            else
            {
                ReadAllFiles(args);
            }

            Console.WriteLine("Press any key to continue...");
            Console.ReadKey();
        }

        public static void ReadAllFiles(String[] filenames)
        {
            foreach (String filename in filenames)
            {
                if (File.Exists(filename))
                {
                    ReadFile(filename);
                }
                else
                {
                    Console.WriteLine("CANNOT FIND FILE "+filename);
                }
            }
            Console.WriteLine();
        }

        public static void ReadAllFiles(String LBXDirectory)
        {
            ReadAllFiles(Directory.GetFiles(LBXDirectory, "*.lbx", SearchOption.TopDirectoryOnly).ToArray());
        }

        private static void ReadFile(String LBXFileName)
        {
            char[] InvalidFilenameChars = Path.GetInvalidFileNameChars();
            uint FileSize = (uint)new FileInfo(LBXFileName).Length;
            ushort FileCount = 0;
            using (BinaryReader br = new BinaryReader(File.OpenRead(LBXFileName), ASCIIEncoding.ASCII))
            {
                // Verify the format's magic word.
                FileCount = br.ReadUInt16();
                byte[] MagicWord = br.ReadBytes(4);
                ushort Info = br.ReadUInt16();

                if(FileCount < 1)
                {
                    Console.WriteLine(Path.GetFileName(LBXFileName) + " contains no files.");
                    return;
                }
                if(MagicWord[0] != (byte)173 || MagicWord[1] != (byte)254 || MagicWord[2] != (byte)0 || MagicWord[3] != (byte)0)
                {
                    Console.WriteLine(Path.GetFileName(LBXFileName) + " is not a SimTex LBX file.");
                    return;
                }
                Console.WriteLine(Path.GetFileName(LBXFileName) + " is a valid file with " + FileCount + " records");

                // Header Structure
                uint[] FileOffsets = new uint[FileCount];
                String[] FileNames = new String[FileCount];
                String[] FileDescriptions = new String[FileCount];

                // Load the offsets.
                for(int i=0;i<FileCount;i++)
                {
                    FileOffsets[i] = br.ReadUInt32();
                }

                // Set the last offset to the end of the file.
                FileOffsets[FileOffsets.Length - 1] = FileSize;

                // Jump to the start of the file names sections.
                br.BaseStream.Position = 512;

                // Load the file names and descriptions.
                bool EndOfNames = false;
                for(int i=0;i<FileCount;i++)
                {
                    // In the LBX format, not every file has a name. It's not uncommon for there to be 6 files, but only two names.
                    // Because of this, this trap prevents us from reading any further than the position of the first file.
                    if (512 + ((i + 1) * 32) > FileOffsets[i])
                    {
                        // 512 is the start of the names/desc, 32 is their combined length.
                        EndOfNames = true;
                    }
                    
                    if (!EndOfNames)
                    {
                        // There are more names to read.
                        FileNames[i] = new String(br.ReadChars(8));

                        br.ReadChar(); // Throw away the null-terminator.
 
                        FileDescriptions[i] = new String(br.ReadChars(22));
 
                        br.ReadChar(); // Throw away the null-terminator.
                    }
                    else
                    {
                        // We've reached the end of the names section, but there may be additional files, so give each a blank name.
                        FileNames[i] = "Unnamed ";
                        FileDescriptions[i] = "";
                    }
                }

                // Extract the files.
                Console.WriteLine("Extracting from " + Path.GetFileName(LBXFileName) + "...");

                String LBXFolder = Path.Combine(Path.GetDirectoryName(LBXFileName), Path.GetFileNameWithoutExtension(LBXFileName));
                if (!Directory.Exists(LBXFolder))
                    Directory.CreateDirectory(LBXFolder);


                for (int i = 0; i < FileCount - 1; i++)
                {
                    // Determine the length of the current file by subtracting the start of the file from the next file.
                    uint FileLength = FileOffsets[i + 1] - FileOffsets[i];
                    br.BaseStream.Position = FileOffsets[i];

                    // Get the base name of the file (LBX files often reuse names in the same path).
                    String BaseName = FileNames[i].Trim();
                    for (int k = 0; k < BaseName.Length; k++)
                    {
                        if (InvalidFilenameChars.Contains(BaseName[k]))
                        {
                            BaseName = "";
                            break;
                        }
                    }
                    //if (String.IsNullOrEmpty(BaseName))
                        BaseName = i.ToString();

                    String descr = FileDescriptions[i];
                    for (int k = 0; k < descr.Length; k++)
                    {
                        if (InvalidFilenameChars.Contains(descr[k]))
                        {
                            descr = "Unknown";
                            break;
                        }
                    }
                    Console.WriteLine("    " + LBXFolder + "/" + BaseName + " - " + descr + "  -  " + FileLength + " bytes");


                    // Read the file data from the LBX.
                    byte[] FileContents = br.ReadBytes((int)FileLength);

                    // Check for reusing the same file name.
                    String OutputFileName;
                    int Addition = -1;
                    do
                    {
                        Addition = Addition + 1;
                        OutputFileName = LBXFolder + "/" + BaseName + ((Addition > 0) ? "-" + Addition : "");
                    }
                    while (File.Exists(OutputFileName));

                    // Output the contents of the file 
                    File.WriteAllBytes(OutputFileName, FileContents);
                }
            }
            Console.WriteLine(FileCount + " file(s) extracted.");
        }

        public static UInt16 ReverseBytes(UInt16 value)
        {
            return (UInt16)((value & 0xFFU) << 8 | (value & 0xFF00U) >> 8);
        }

        public static UInt32 ReverseBytes(UInt32 value)
        {
            return (value & 0x000000FFU) << 24 | (value & 0x0000FF00U) << 8 |
                   (value & 0x00FF0000U) >> 8 | (value & 0xFF000000U) >> 24;
        }
    }
}
