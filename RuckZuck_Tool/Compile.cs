using Microsoft.CSharp;
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RuckZuck_Tool
{
    class CreateExe
    {
        public CSharpCodeProvider provider
        {
            get; set;
        }

        public CompilerParameters cp
        {
            get; set;
        }

        public List<string> Sources
        {
            get; set;
        }

        private byte[] _icon;
        private FileInfo _iconFile;

        public byte[] Icon
        {
            get
            {
                return _icon;
            }
            set
            {
                _icon = value;

                if (value == null)
                    return;

                if (_iconFile == null || !_iconFile.Exists)
                {
                    _iconFile = new FileInfo(Environment.ExpandEnvironmentVariables(@"%TEMP%\" + Path.GetRandomFileName().Split('.')[0] + ".ico"));

                    Convert(value, Environment.ExpandEnvironmentVariables(_iconFile.FullName), 64, true);

                    cp.CompilerOptions = Environment.ExpandEnvironmentVariables(@"/win32icon:" + _iconFile.FullName + " /optimize");
                }
            }
        }

        

        public CreateExe(string exeFile)
        {
            provider = new CSharpCodeProvider();

            // Build the parameters for source compilation.
            cp = new CompilerParameters();

            Sources = new List<string>();

            // Add an assembly reference.
            cp.ReferencedAssemblies.Add("System.dll");
            cp.ReferencedAssemblies.Add("System.Core.dll");
            cp.ReferencedAssemblies.Add("System.Xml.dll");
            cp.ReferencedAssemblies.Add("System.Runtime.Serialization.dll");
            cp.ReferencedAssemblies.Add("System.Web.dll");
            cp.ReferencedAssemblies.Add("System.Web.Services.dll");

            cp.ReferencedAssemblies.Add("System.Drawing.dll");
            cp.ReferencedAssemblies.Add("System.Data.DataSetExtensions.dll");
            cp.ReferencedAssemblies.Add("System.Data.dll");
            cp.ReferencedAssemblies.Add("System.EnterpriseServices.dll");
            cp.ReferencedAssemblies.Add("System.Xml.Linq.dll");

            cp.ReferencedAssemblies.Add("System.Net.Http.dll");
            cp.ReferencedAssemblies.Add("System.Web.Extensions.dll");

            cp.ReferencedAssemblies.Add(@"C:\Windows\Microsoft.NET\assembly\GAC_MSIL\System.Management.Automation\v4.0_3.0.0.0__31bf3856ad364e35\System.Management.Automation.dll");

            // Generate an executable instead of
            // a class library.
            cp.GenerateExecutable = true;

            // Set the assembly file name to generate.
            cp.OutputAssembly = exeFile;

            // Save the assembly as a physical file.
            cp.GenerateInMemory = false;

            cp.IncludeDebugInformation = false;
            //cp.CompilerOptions = Environment.ExpandEnvironmentVariables(@"/win32icon:%TEMP%\RZWrapper.ico /optimize");

        }

        public bool Compile()
        {
            if (Sources.Count == 0)
                return false;

            if (_iconFile == null)
            {
                Icon = _icon;  //recreate the .ico File
            }

            // Invoke compilation.
            CompilerResults cr = provider.CompileAssemblyFromSource(cp, Sources.ToArray());

            if (cr.Errors.Count > 0)
            {
                // Display compilation errors.
                Console.WriteLine("Errors building Source into {0}", cr.PathToAssembly);
                foreach (CompilerError ce in cr.Errors)
                {
                    Console.WriteLine("  {0}", ce.ToString());
                    Console.WriteLine();
                }
            }
            else
            {
                Console.WriteLine("Source built into {0} successfully.", cr.PathToAssembly);
            }

            if (_iconFile.Exists)
                _iconFile.Delete();

            // Return the results of compilation.
            if (cr.Errors.Count > 0)
            {
                return false;
            }
            else
            {
                return true;
            }
        }

        public static bool Convert(System.IO.Stream input_stream, System.IO.Stream output_stream, int size, bool keep_aspect_ratio = false)
        {
            System.Drawing.Bitmap input_bit = (System.Drawing.Bitmap)System.Drawing.Bitmap.FromStream(input_stream);
            if (input_bit != null)
            {
                int width, height;
                if (keep_aspect_ratio)
                {
                    width = size;
                    height = input_bit.Height / input_bit.Width * size;
                }
                else
                {
                    width = height = size;
                }
                System.Drawing.Bitmap new_bit = new System.Drawing.Bitmap(input_bit, new System.Drawing.Size(width, height));
                if (new_bit != null)
                {
                    // save the resized png into a memory stream for future use
                    System.IO.MemoryStream mem_data = new System.IO.MemoryStream();
                    new_bit.Save(mem_data, System.Drawing.Imaging.ImageFormat.Png);

                    System.IO.BinaryWriter icon_writer = new System.IO.BinaryWriter(output_stream);
                    if (output_stream != null && icon_writer != null)
                    {
                        // 0-1 reserved, 0
                        icon_writer.Write((byte)0);
                        icon_writer.Write((byte)0);

                        // 2-3 image type, 1 = icon, 2 = cursor
                        icon_writer.Write((short)1);

                        // 4-5 number of images
                        icon_writer.Write((short)1);

                        // image entry 1
                        // 0 image width
                        icon_writer.Write((byte)width);
                        // 1 image height
                        icon_writer.Write((byte)height);

                        // 2 number of colors
                        icon_writer.Write((byte)0);

                        // 3 reserved
                        icon_writer.Write((byte)0);

                        // 4-5 color planes
                        icon_writer.Write((short)0);

                        // 6-7 bits per pixel
                        icon_writer.Write((short)32);

                        // 8-11 size of image data
                        icon_writer.Write((int)mem_data.Length);

                        // 12-15 offset of image data
                        icon_writer.Write((int)(6 + 16));

                        // write image data
                        // png data must contain the whole png data file
                        icon_writer.Write(mem_data.ToArray());

                        icon_writer.Flush();

                        return true;
                    }
                }
                return false;
            }
            return false;
        }

        public static bool Convert(byte[] input_image, string output_icon, int size, bool keep_aspect_ratio = false)
        {
            try
            {
                MemoryStream input_stream = new MemoryStream(input_image);
                FileStream output_stream = new System.IO.FileStream(output_icon, System.IO.FileMode.OpenOrCreate);

                bool result = Convert(input_stream, output_stream, size, keep_aspect_ratio);

                input_stream.Close();
                output_stream.Close();

                return result;
            }
            catch { }

            return false;
        }
    }
}
