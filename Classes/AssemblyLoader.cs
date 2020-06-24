using System;
using System.Linq;
using System.IO;
using System.Reflection;
using System.IO.Compression;

namespace ASTAWebServer
{
    internal static class AssemblyLoader
    {
        //https://stackoverrun.com/ru/q/4794740
        internal static void RegisterAssemblyLoader()
        {
            AppDomain currentDomain = AppDomain.CurrentDomain;
            currentDomain.AssemblyResolve -= OnResolveAssembly;
            currentDomain.AssemblyResolve += OnResolveAssembly;
        }

        private static Assembly OnResolveAssembly(object sender, ResolveEventArgs args)
        {
            return LoadAssemblyFromManifest(args.Name);
        }

        private static Assembly LoadAssemblyFromManifest(string targetAssemblyName)
        {
            Assembly executingAssembly = Assembly.GetExecutingAssembly();
            byte[] assemblyRawBytes = null;

            //var names = typeof(Program).Assembly.GetManifestResourceNames();
            //foreach (var n in names)
            //{
            //    log.WriteString("n: " + n);
            //}

            try
            {
                AssemblyName assemblyName = new AssemblyName(targetAssemblyName);

                string resourceName = DetermineEmbeddedResourceName(assemblyName, executingAssembly);

                using (Stream stream = executingAssembly.GetManifestResourceStream(resourceName))
                {
                    if (stream == null)
                    {
                        return null;
                    }

                    using (var deflated = new DeflateStream(stream, CompressionMode.Decompress))
                    using (var reader = new BinaryReader(deflated))
                    {
                        var one_megabyte = 1024 * 1024;
                        assemblyRawBytes = reader.ReadBytes(one_megabyte);
                    }
                }
            }
            catch { }

            return Assembly.Load(assemblyRawBytes);
        }

        private static string DetermineEmbeddedResourceName(AssemblyName assemblyName, Assembly executingAssembly)
        {
            //This assumes you have the assemblies in a folder named "Resources"
            string resourceName = $"{executingAssembly.GetName().Name}.Resources.{assemblyName.Name}.dll.deflated";

            //This logic finds the assembly manifest name even if it's not an case match for the requested assembly                          
            var matchingResource = executingAssembly
                .GetManifestResourceNames()
                .FirstOrDefault(res => res.ToLower() == resourceName.ToLower());

            if (matchingResource != null)
            {
                resourceName = matchingResource;
            }
            return resourceName;
        }
    }
}
