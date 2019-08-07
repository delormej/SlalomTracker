using System;
using System.IO;
using FFmpeg.NET;
using FFmpeg.NET.Events;
using System.Threading;
using System.Threading.Tasks;

namespace SlalomTracker
{
    public class VideoTasks
    {
        FFmpeg.NET.Engine _ffmpeg;

        public VideoTasks()
        {
            _ffmpeg = new Engine("ffmpeg");
            _ffmpeg.Error += OnError;
        }

        public async Task<string> TrimAsync(string localVideoPath, double start, double length)
        {
            var inputFile = new MediaFile (localVideoPath);
            var outputFile = new MediaFile (AppendToFileName(localVideoPath, "_t"));
            var options = new ConversionOptions();            
            options.CutMedia(TimeSpan.FromSeconds(start), TimeSpan.FromSeconds(length));   
            
            try 
            {
                await _ffmpeg.ConvertAsync(inputFile, outputFile, options); 
                Console.WriteLine($"Converted: {outputFile.FileInfo.FullName}");
            }
            catch (Exception e)
            {
                throw new ApplicationException(
                    "Unable to trim ski video, is FFMPEG installed?",
                    e
                );
            }
            return outputFile.FileInfo.FullName;
        }

        public async Task<string> RemoveAudioAsync(string inputFile)
        {
            string outputFile = AppendToFileName(inputFile, "s");
            string parameters = $"-i {inputFile} -c copy -an {outputFile}";
            await _ffmpeg.ExecuteAsync(parameters);
            Console.WriteLine($"Removed Audio: {outputFile}");
            
            return outputFile;
        }

        public DateTime GetCreationTime(string inputFile)
        {
            string output = GetFFPmegOutput(inputFile);
            return ParseCreationDate(output);
        }        

        /// <summary>
        /// Generates a thumbnail image at the seconds specified.  Returns the path.
        /// </summary>
        public async Task<string> GetThumbnailAsync(string videoLocalPath, double atSeconds)
        {
            if (!videoLocalPath.ToUpper().EndsWith(".MP4"))
                throw new ApplicationException($"Cannot generate thumbnail, invalid video path: {videoLocalPath}");
            string thumbnailPath = videoLocalPath.ToUpper().Replace(".MP4", ".PNG");

            var inputFile = new MediaFile (videoLocalPath);
            var outputFile = new MediaFile(thumbnailPath);
            var options = new ConversionOptions { Seek = TimeSpan.FromSeconds(atSeconds) };

            Console.WriteLine($"Generating thumbnail: {thumbnailPath} for video: {videoLocalPath} at {atSeconds} seconds.");
            await _ffmpeg.GetThumbnailAsync(inputFile, outputFile, options);
            Console.WriteLine($"Generated thumbnail: {thumbnailPath}");

            return thumbnailPath;
        }

        private void OnError(object sender, ConversionErrorEventArgs e)
        {
            Console.WriteLine("Trim video error: [{0} => {1}]: Error: {2}\n{3}", e.Input.FileInfo.Name, e.Output.FileInfo.Name, e.Exception.ExitCode, e.Exception.InnerException);
        }    

        private string AppendToFileName(string inputFile, string suffix)
        {
            int start = inputFile.LastIndexOf(".MP4");
            if (start <= 0)
                throw new ApplicationException($"Could not generate an output filename from {inputFile}.");

            string outputFile = $"{inputFile.Substring(0, start)}{suffix}.MP4";
            return outputFile;
        }    

        private string GetFFPmegOutput(string inputFile)
        {
            string parameters = $"-i {inputFile}";
            System.Diagnostics.Process ffmpegProcess = System.Diagnostics.Process.Start("ffmpeg", parameters);
            ffmpegProcess.WaitForExit(2000);
            string output = ffmpegProcess.StandardOutput.ReadToEnd();
            Console.WriteLine(output);
            return output;            
        }

        private DateTime ParseCreationDate(string input)
        {
            // Read and trim start of each line until it begins with: creation_time
            // Then parse after the ':'
            //      creation_time   : 2019-07-11T07:13:55.000000Z
            DateTime creationTime = DateTime.MinValue;
            using (StringReader reader = new StringReader(input))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (line.Trim().StartsWith("creation_time")) 
                    {
                        string[] values = line.Split(':');
                        if (values.Length > 1) 
                        {
                            string rawDate = values[1].Trim();
                            creationTime = DateTime.Parse(rawDate);
                            break;
                        }
                    }
                }
            }

            if (creationTime == DateTime.MinValue)
            {
                throw new ApplicationException($"Unable to find creation_time in output:\n {input}");
            }
            return creationTime;
        }
    }
}
