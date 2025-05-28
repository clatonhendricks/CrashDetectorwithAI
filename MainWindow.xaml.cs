using System;
using System.Diagnostics;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Security;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Linq;

namespace CrashDetectorwithAI
{
    public partial class MainWindow : Window
    {
        private string faultyBucketText = "Unknown";
        private string modelPath = string.Empty;
        private Phi4ModelService? modelService = null;

        public MainWindow()
        {
            InitializeComponent();
            LoadSettings();
            PositionWindowAtBottomRight();
            LoadCrashDetails();
        }

        private void LoadSettings()
        {
            try
            {
                string appSettingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appSettings.json");
                if (File.Exists(appSettingsPath))
                {
                    string jsonContent = File.ReadAllText(appSettingsPath);
                    JObject settings = JObject.Parse(jsonContent);
                             // Try to get the modelPath property
            JToken? modelPathToken = settings["modelPath"];
            if (modelPathToken != null && modelPathToken.Type == JTokenType.String)
            {
                // Get the path and normalize it
                modelPath = Path.GetFullPath(Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    modelPathToken.ToString().TrimStart('\\')
                ));
            }
                }

                if (string.IsNullOrEmpty(modelPath))
                {
                    MessageBox.Show("Model path not found in appSettings.json. Please update the configuration file with a valid model path.", 
                        "Configuration Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading settings: {ex.Message}", "Settings Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void PositionWindowAtBottomRight()
        {
            var screenWidth = SystemParameters.WorkArea.Width;
            var screenHeight = SystemParameters.WorkArea.Height;
            this.Left = screenWidth - this.Width - 10; // 10px margin from the right
            this.Top = screenHeight - this.Height - 10; // 10px margin from the bottom
        }

        private void LoadCrashDetails()
        {
            try
            {
                bool foundEvent = false;
                
                // Log the attempt to access event logs
                CrashDetailsText.Text = "Searching for crash events...";
                
                // Create an EventLog instance and query the Application log
                using (EventLog eventLog = new EventLog("Application"))
                {
                    // Show information about the log we're searching
                    int entryCount = eventLog.Entries.Count;
                    
                    // Iterate through event log entries in reverse (most recent first)
                    for (int i = entryCount - 1; i >= Math.Max(0, entryCount - 1000); i--)
                    {
                        try
                        {
                            EventLogEntry entry = eventLog.Entries[i];
                            
                            // Check if this is a Windows Error Reporting event with ID 1001 and LiveKernelEvent in the message
                            if (entry.InstanceId == 1001 && 
                                string.Equals(entry.Source, "Windows Error Reporting", StringComparison.OrdinalIgnoreCase) && 
                                entry.Message != null &&
                                entry.Message.Contains("LiveKernelEvent", StringComparison.OrdinalIgnoreCase))
                            {
                                // We found a matching event
                                string crashTime = entry.TimeGenerated.ToString("g"); // Short date/time format
                                faultyBucketText = ExtractFaultyBucketText(entry.Message);
                                
                                CrashDetailsText.Text = $"Crash Time: {crashTime}\n\nFaulty Bucket:\n{faultyBucketText}";
                                foundEvent = true;
                                break;
                            }
                        }
                        catch
                        {
                            // Skip this entry if it causes an error
                            continue;
                        }
                    }

                    if (!foundEvent)
                    {
                        // Display default text if no events are found
                        CrashDetailsText.Text = "No relevant crash events found.\n\nLooking for Windows Error Reporting events with ID 1001 containing 'LiveKernelEvent'.";
                    }
                }
            }
            catch (SecurityException ex)
            {
                MessageBox.Show($"Security error: {ex.Message}\n\nPlease run the application as administrator to access event logs.", "Access Denied", MessageBoxButton.OK, MessageBoxImage.Warning);
                CrashDetailsText.Text = "Error: Administrative privileges required to read event logs.";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading crash details: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                CrashDetailsText.Text = $"Error loading crash details.\n\n{ex.Message}";
            }
        }

        private string ExtractFaultyBucketText(string message)
        {
            try
            {
                // Extract the FaultyBucketText from the message
                int startIndex = message.IndexOf("Fault bucket", StringComparison.OrdinalIgnoreCase);
                if (startIndex >= 0)
                {
                    int endIndex = message.IndexOf("\r\n", startIndex);
                    if (endIndex > startIndex)
                    {
                        return message.Substring(startIndex, endIndex - startIndex).Trim();
                    }
                    return message.Substring(startIndex).Trim();
                }
                return "No fault bucket information found";
            }
            catch
            {
                return "Error extracting fault bucket information";
            }
        }

        private async void AskPhillyButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Show loading indicator and disable button
                CrashDetailsText.Text += "\n\nAsking PHILLY for help...";
                var askPhillyButton = sender as Button;
                if (askPhillyButton != null)
                {
                    askPhillyButton.IsEnabled = false;
                }

                // Verify that we have a model path
                if (string.IsNullOrEmpty(modelPath))
                {
                    throw new InvalidOperationException("Model path is not configured. Please update appSettings.json with a valid model path.");
                }

                // Check if the model path exists
                if (!Directory.Exists(modelPath))
                {
                    throw new DirectoryNotFoundException($"Model directory not found: {modelPath}");
                }

                // Create the prompt with the faulty bucket information
                string systemPrompt = "You are a helpful Windows technical support assistant. Provide brief explanations and direct solutions.";
                string userPrompt = $"Can you briefly explain what this bucket error code means and how to resolve in short brief steps: {faultyBucketText}";
                string fullPrompt = Phi4ModelService.FormatPrompt(systemPrompt, userPrompt);
                
                // Initialize model service if not already created
                if (modelService == null)
                {
                    // Create the model service with a logging action that updates the UI
                    modelService = new Phi4ModelService(modelPath, message => 
                    {
                        Dispatcher.Invoke(() => CrashDetailsText.Text += $"\n{message}");
                    });
                    
                    // Initialize the model
                    await modelService.InitializeAsync();
                }

                // Ensure the window height can accommodate the response
                EnsureWindowSizeForResponse();

                // Get response from Phi model
                if (modelService.IsInitialized)
                {
                    string response = await modelService.GenerateResponseAsync(fullPrompt);
                    CrashDetailsText.Text = $"Crash Analysis:\n\nFaulty Bucket: {faultyBucketText}\n\nPHILLY's Response:\n{response}";
                }
                else
                {
                    // Fallback to predefined response if model not loaded
                    CrashDetailsText.Text = $"Crash Analysis:\n\nFaulty Bucket: {faultyBucketText}\n\nPHILLY's Response:\n{GetFallbackResponse()}\n\n(Model could not be loaded)";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error querying Phi model: {ex.Message}", "Model Error", MessageBoxButton.OK, MessageBoxImage.Error);
                CrashDetailsText.Text += $"\n\nError querying Phi model: {ex.Message}";
            }
            finally
            {
                // Re-enable button
                var askPhillyButton = sender as Button;
                if (askPhillyButton != null)
                {
                    askPhillyButton.IsEnabled = true;
                }
            }
        }

        private void EnsureWindowSizeForResponse()
        {
            // Ensure window is at least 400px wide for comfortable reading
            if (this.Width < 400)
            {
                this.Width = 400;
            }
            
            // Allow the window to grow in height to show response
            this.MinHeight = 250;
            
            // Adjust the window position if it might go off-screen
            var screenHeight = SystemParameters.WorkArea.Height;
            if (this.Top + 400 > screenHeight)
            {
                this.Top = Math.Max(10, screenHeight - 400);
            }
        }

        private string GetFallbackResponse()
        {
            // Provide useful responses based on patterns in the fault bucket text
            string response = "";
                    
            // Check for common error patterns in fault bucket text
            if (faultyBucketText.Contains("LiveKernelEvent", StringComparison.OrdinalIgnoreCase))
            {
                response = "This is a Windows kernel crash (blue screen error).\n\nPossible causes:\n1. Driver issues\n2. Hardware problems\n3. System instability\n\nResolution steps:\n1. Update all device drivers\n2. Run system diagnostics\n3. Check for Windows updates\n4. If persistent, contact system manufacturer";
            }
            else if (faultyBucketText.Contains("DRIVER", StringComparison.OrdinalIgnoreCase) || faultyBucketText.Contains(".sys", StringComparison.OrdinalIgnoreCase))
            {
                response = "This appears to be a driver-related crash.\n\nResolution steps:\n1. Identify the mentioned driver\n2. Update the driver from manufacturer's website\n3. If problems persist, try uninstalling the driver\n4. Contact driver vendor support";
            }
            else if (faultyBucketText.Contains("MEMORY", StringComparison.OrdinalIgnoreCase) || faultyBucketText.Contains("HEAP", StringComparison.OrdinalIgnoreCase))
            {
                response = "This appears to be a memory-related crash.\n\nResolution steps:\n1. Run Windows Memory Diagnostic\n2. Check for application updates\n3. Verify sufficient disk space\n4. Consider increasing virtual memory allocation";
            }
            else
            {
                response = "Based on the error information, this might be a system crash.\n\nGeneral troubleshooting steps:\n1. Update Windows and all drivers\n2. Run System File Checker (sfc /scannow)\n3. Check for hardware issues\n4. Review system event logs for additional errors";
            }

            return response;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            // Dispose of the model service if it exists
            modelService?.Dispose();
            
            Close();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // Allow the window to be dragged when the user clicks and holds the title bar
            this.DragMove();
        }
    }
}