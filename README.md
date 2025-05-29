[![Build](https://github.com/clatonhendricks/CrashDetectorwithAI/actions/workflows/build.yml/badge.svg)](https://github.com/clatonhendricks/CrashDetectorwithAI/actions/workflows/build.yml)
 
 # CrashDetectorwithAI

A Windows application that detects system crashes and uses AI (Phi-4 model) to provide explanations and solutions for crash events.

## Features

- Automatically detects Windows crash events
- Extracts fault bucket information from Windows Error Reporting
- Uses the Phi-4 AI model to analyze crash data and provide solutions
- Modern, sleek UI with responsive design

## Technical Details

- Built with WPF (.NET 9.0)
- Uses ONNX Runtime for AI model inference
- Implements Phi-4 model integration for crash analysis

## Requirements

- Windows operating system
- .NET 9.0 runtime
- Phi-4 model files (configured in appSettings.yml)

## Setup

1. Clone the repository
2. Update the `appSettings.yml` with the path to your Phi-4 model
3. Build and run the application

## Configuration

In `appSettings.yml`, set the `modelPath` to the location of your Phi-4 model files:

```yaml
# Path to the Phi-4 model directory
modelPath: C:\Path\To\Your\Phi4Model
```

YAML configuration allows you to use regular Windows paths with backslashes without needing to escape them.
