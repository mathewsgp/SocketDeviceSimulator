# Socket Simulator

A C# WPF desktop application to simulate TCP/IP command/response protocols for developers and testers.

## Features

### Connection
- **TCP Server/Client**: Switch between server and client modes
- **Configurable IP and Port**: Set your desired connection parameters
- **Connect/Disconnect**: Easy connection management
- **Connection Status**: Visual indicator of connection state

### Protocol Definition
- **JSON Protocol Loading**: Load protocol definitions from JSON files
- **Variable Substitution**: Use `${Variable}` syntax in templates
- **Command Parameters**: Define parameters with types and possible values

### Scenario Editor
Supports the following step types:
- **Wait for Command**: Wait for a specific command with timeout
- **Send Response**: Send a response to a command
- **Send Command**: Send a custom command
- **Delay**: Pause execution for a specified duration
- **Set Variable**: Set, increment, decrement, or toggle variables
- **If/Else**: Conditional execution based on variable values

### Variables
Global runtime variables:
- State, Temperature, Pressure, Counter (predefined)

Operations:
- Set, Get, Increment, Decrement, Toggle

### UI Panels
- **Connection Panel**: Configure and manage TCP connections
- **Protocol Explorer**: Browse loaded protocol commands
- **Scenario Editor**: Create and edit test scenarios
- **Variable Window**: View and manage runtime variables
- **Communication Log**: Real-time log of all communications
- **Manual Console**: Send custom commands manually

### File Operations
- **Protocol JSON**: Load/save protocol definitions
- **Project (.simproj)**: Save and load complete project configurations
- **Open/Save/Save As**: Standard file operations with Ctrl+S, Ctrl+O, Ctrl+N

### Logging
- RX/TX messages
- Connection events
- Errors
- Variable changes

## Architecture
- **MVVM Pattern**: Clean separation of concerns
- **Modular Services**: Socket, Protocol, Scenario, Variable, Logging services
- **Extensible Design**: Easy to add new step types and features

## Getting Started

### Running the Application
1. Navigate to the `publish` folder
2. Run `SocketSimulator.exe`

### Creating a Protocol
1. Click "New Protocol" in the Protocol Explorer
2. Add commands with "Add Command"
3. Define response templates using `${Variable}` syntax
4. Save the protocol as JSON

### Creating a Scenario
1. Use the toolbar buttons to add steps:
   - Wait Command: Wait for incoming commands
   - Send Response: Respond to commands
   - Send Command: Send commands
   - Delay: Add timing delays
   - Set Variable: Modify variables
   - If/Else: Add conditional logic
2. Reorder steps using ↑ and ↓ buttons
3. Run the scenario with "Run"

### Connecting
1. Choose Server or Client mode
2. Set IP address and port
3. Click "Connect"
4. Monitor the Communication Log for activity

## Sample Protocol Format

```json
{
  "Name": "Sample Protocol",
  "Version": "1.0",
  "Commands": [
    {
      "Name": "LOGIN",
      "Description": "User login",
      "Parameters": [
        { "Name": "username", "Type": "string", "Required": true }
      ],
      "ResponseTemplate": "LOGIN_OK user=${username}"
    }
  ]
}
```

## Variable Substitution

Variables in templates are replaced at runtime:
- `${State}` - Current state value
- `${Temperature}` - Current temperature
- `${Pressure}` - Current pressure
- `${Counter}` - Current counter value
- `${SessionId}` - Custom session ID

## Requirements
- Windows with .NET 8.0 Runtime
- For development: Visual Studio 2022 or .NET 8.0 SDK

## Building from Source

```bash
cd SocketSimulator
dotnet restore
dotnet build
dotnet run
```

## License
MIT License
