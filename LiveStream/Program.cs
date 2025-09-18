using System;
using System.Diagnostics;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.IO; // Add missing using statement for File operations

class Program
{
    private static readonly SemaphoreSlim frameFileLock = new SemaphoreSlim(1, 1);

    static async Task Main(string[] args)
    {
        Console.WriteLine("Starting libcamera live stream server...");
        
        // Configuration
        string streamUrl = "http://localhost:8080/";
        int port = 8080;
        
        // Create cancellation token for graceful shutdown
        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (s, e) => {
            e.Cancel = true;
            cts.Cancel();
            Console.WriteLine("Shutting down server...");
            // Delete the frame.jpg file if it exists
            try {
                if (File.Exists("./frame.jpg")) {
                    File.Delete("./frame.jpg");
                }
            } catch (Exception ex) {
                Console.WriteLine($"Failed to clean up frame.jpg: {ex.Message}");
            }
            // Force program termination after some reasonable timeout
            Task.Run(async () => {
                await Task.Delay(2000); // Give 2 seconds for graceful shutdown
                Environment.Exit(0);    // Force exit if still running
            });
        };
        
        // Start HTTP server in background
        var serverTask = StartHttpServerAsync(port, cts.Token);
        
        // Start libcamera stream
        var streamTask = StartLibcameraStreamAsync(cts.Token);
        
        Console.WriteLine($"Stream is available at {streamUrl}");
        Console.WriteLine("Press Ctrl+C to exit");
        
        // Wait for cancellation
        try
        {
            await Task.WhenAll(serverTask, streamTask);
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("Stream terminated");
        }
    }
    
    static async Task StartHttpServerAsync(int port, CancellationToken token)
    {
        var listener = new HttpListener();
        listener.Prefixes.Add($"http://*:{port}/");
        listener.Start();
        
        Console.WriteLine($"HTTP server started on port {port}");
        
        try
        {
            while (!token.IsCancellationRequested)
            {
                var contextTask = listener.GetContextAsync();
                var context = await contextTask;
                
                // Handle the request in a separate task
                _ = Task.Run(() => ProcessRequestAsync(context), token);
            }
        }
        finally
        {
            listener.Stop();
        }
    }
    
    static async Task ProcessRequestAsync(HttpListenerContext context)
    {
        try
        {
            var response = context.Response;
            
            // Set up response for MJPEG stream
            if (context.Request.Url.LocalPath == "/")
            {
                // HTML code remains the same
                response.ContentType = "text/html";
                var html = @"
                    <html>
                        <head><title>libcamera Live Stream</title></head>
                        <body>
                            <h1>libcamera Live Stream</h1>
                            <img src=""/stream"" />
                        </body>
                    </html>";
                var buffer = System.Text.Encoding.UTF8.GetBytes(html);
                await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            }
            else if (context.Request.Url.LocalPath == "/stream")
            {
                response.ContentType = "multipart/x-mixed-replace; boundary=frame";
                response.Headers.Add("Cache-Control", "no-cache");
                response.Headers.Add("Connection", "keep-alive");
                response.Headers.Add("Pragma", "no-cache");
                
                // Keep connection open for streaming
                while (true)
                {
                    try
                    {
                        byte[] frameData = null;
                        
                        // Read the latest frame from the shared location with locking
                        if (File.Exists("./frame.jpg"))
                        {
                            try
                            {
                                await frameFileLock.WaitAsync();
                                try
                                {
                                    frameData = await File.ReadAllBytesAsync("./frame.jpg");
                                }
                                finally
                                {
                                    frameFileLock.Release();
                                }
                            }
                            catch (IOException ioEx)
                            {
                                Console.WriteLine($"File access error: {ioEx.Message}");
                                await Task.Delay(10); // Quick retry
                                continue;
                            }
                            
                            if (frameData != null && frameData.Length > 0)
                            {
                                try
                                {
                                    // Write multipart boundary
                                    var header = $"\r\n--frame\r\nContent-Type: image/jpeg\r\nContent-Length: {frameData.Length}\r\n\r\n";
                                    var headerData = System.Text.Encoding.ASCII.GetBytes(header);
                                    await response.OutputStream.WriteAsync(headerData);
                                    
                                    // Write frame data
                                    await response.OutputStream.WriteAsync(frameData);
                                    await response.OutputStream.FlushAsync();
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"Stream output error: {ex.Message}");
                                    break; // Exit on write/flush errors
                                }
                            }
                        }
                        
                        // Add fixed delay between frames instead of variable timing
                        await Task.Delay(33); // ~30fps
                        
                        // Simple connection check
                        try
                        {
                            var checkTask = response.OutputStream.FlushAsync();
                            if (await Task.WhenAny(checkTask, Task.Delay(500)) != checkTask)
                            {
                                Console.WriteLine("Client disconnect detected (timeout)");
                                break;
                            }
                        }
                        catch
                        {
                            Console.WriteLine("Client disconnect detected (error)");
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log error but don't break the stream unless it's due to connection
                        Console.WriteLine($"Stream error: {ex.Message}");
                        
                        if (ex is HttpListenerException || 
                            ex is IOException || 
                            ex.Message.Contains("disposed") || 
                            ex.Message.Contains("closed"))
                        {
                            break; // Exit on connection errors
                        }
                        
                        await Task.Delay(100); // Brief pause before retry
                    }
                }
                
                Console.WriteLine("Stream connection ended");
            }
            else
            {
                response.StatusCode = 404;
            }
            
            response.Close();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Request processing error: {ex.Message}");
            try { context.Response.Close(); } catch { }
        }
    }
    
    static async Task StartLibcameraStreamAsync(CancellationToken token)
    {
        // Create a process to run libcamera-vid
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = "libcamera-vid",
            Arguments = "--width 4608 --height 2592 -o - --codec mjpeg --inline --timeout 0 --nopreview",
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        process.Start();
        
        // Increased buffer size significantly to handle high resolution frames
        byte[] frameBuffer = new byte[8 * 1024 * 1024]; // 8MB buffer (was 500KB)
        int bufferPos = 0;
        
        // Read the output stream from libcamera-vid
        while (!token.IsCancellationRequested)
        {
            try
            {
                // Check if buffer is nearly full and needs expansion
                if (bufferPos > frameBuffer.Length * 0.9)
                {
                    Console.WriteLine("Expanding buffer size to accommodate larger frames");
                    byte[] newBuffer = new byte[frameBuffer.Length * 2];
                    Array.Copy(frameBuffer, newBuffer, bufferPos);
                    frameBuffer = newBuffer;
                    Console.WriteLine($"Buffer expanded to {frameBuffer.Length / (1024 * 1024)}MB");
                }
                
                int bytesRead = await process.StandardOutput.BaseStream.ReadAsync(frameBuffer, bufferPos, frameBuffer.Length - bufferPos, token);
                
                if (bytesRead <= 0)
                {
                    // Handle end of stream or error
                    Console.WriteLine("End of stream or read error, restarting process");
                    break;
                }
                
                bufferPos += bytesRead;
                
                // Process all complete frames in the buffer
                bool foundFrame = false;
                while (bufferPos > 2 && TryExtractJpegFrame(frameBuffer, bufferPos, out int frameStart, out int frameLength))
                {
                    foundFrame = true;
                    
                    // Extract frame and save to file for the web server to serve
                    byte[] frame = new byte[frameLength];
                    Array.Copy(frameBuffer, frameStart, frame, 0, frameLength);
                    
                    // Use the frameFileLock when writing to the file
                    await frameFileLock.WaitAsync();
                    try
                    {
                        await File.WriteAllBytesAsync("./frame.jpg", frame, token);
                    }
                    finally
                    {
                        frameFileLock.Release();
                    }
                    
                    // Shift remaining data to the beginning of the buffer
                    int remaining = bufferPos - (frameStart + frameLength);
                    if (remaining > 0)
                    {
                        Array.Copy(frameBuffer, frameStart + frameLength, frameBuffer, 0, remaining);
                    }
                    bufferPos = remaining;
                }
                
                // If the buffer is filling up but we haven't found frames for a while
                if (!foundFrame && bufferPos > frameBuffer.Length * 0.8)
                {
                    Console.WriteLine("Buffer filling without finding valid frames, discarding oldest data");
                    
                    // Keep the most recent half of the buffer which is more likely to contain a valid frame start
                    int halfSize = bufferPos / 2;
                    Array.Copy(frameBuffer, halfSize, frameBuffer, 0, halfSize);
                    bufferPos = halfSize;
                }
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                Console.WriteLine($"Error processing camera stream: {ex.Message}");
                await Task.Delay(1000, token); // Brief delay before retry
            }
        }
        
        // Kill the process when done
        try
        {
            if (!process.HasExited)
            {
                process.Kill();
                Console.WriteLine("Camera process terminated");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error terminating process: {ex.Message}");
        }
    }
    
    static bool TryExtractJpegFrame(byte[] buffer, int length, out int start, out int frameLength)
    {
        start = 0;
        frameLength = 0;
        
        // Look for JPEG start marker (0xFF 0xD8)
        for (int i = 0; i < length - 1; i++)
        {
            if (buffer[i] == 0xFF && buffer[i + 1] == 0xD8)
            {
                start = i;
                
                // Look for JPEG end marker (0xFF 0xD9) after start marker
                for (int j = i + 2; j < length - 1; j++)
                {
                    if (buffer[j] == 0xFF && buffer[j + 1] == 0xD9)
                    {
                        // Found complete frame
                        frameLength = (j + 2) - start;
                        return true;
                    }
                }
                break;
            }
        }
        
        return false;
    }
}
