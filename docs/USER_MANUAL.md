# Socket Device Simulator - User Manual

## Table of Contents

1. [Introduction](#introduction)
2. [Getting Started](#getting-started)
3. [Application Layout](#application-layout)
4. [Connection Settings](#connection-settings)
5. [Variables Panel](#variables-panel)
6. [Scenario Editor](#scenario-editor)
   - [Step Types](#step-types)
   - [Creating Steps](#creating-steps)
   - [Editing Steps](#editing-steps)
   - [Reordering Steps](#reordering-steps)
7. [Protocol Definition](#protocol-definition)
8. [Manual Send](#manual-send)
9. [Communication Log](#communication-log)
10. [Running a Scenario](#running-a-scenario)
11. [Sample Projects](#sample-projects)
12. [Tips and Best Practices](#tips-and-best-practices)

---

## Introduction

Socket Device Simulator is a desktop application for simulating socket-based device communication. It allows you to:

- Create automated scenarios for device simulation
- Define custom communication protocols
- Test client applications without physical devices
- Debug socket-based communications
- Variable interpolation in messages

---

## Getting Started

1. **Launch the Application**
   - Run `SocketDeviceSimulator.exe`

2. **Load or Create a Project**
   - Use **File → New Project** to start fresh
   - Use **File → Open Project** to load an existing `.simproj` file
   - Use **File → Open Sample** to open one of the built-in examples

3. **Configure Connection**
   - Set IP address and port in the Connection panel
   - Choose Server or Client mode

4. **Create or Load a Scenario**
   - Design your simulation scenario
   - Or load a pre-built sample

---

## Application Layout

```
┌─────────────────────────────────────────────────────────────┐
│  File   Edit   Help                                         │
├─────────────────────────────────────────────────────────────┤
│  Connection: [Server ▼] IP: [127.0.0.1] Port: [9000] [Start] │
├───────────────────────────────┬─────────────────────────────┤
│  Variables                    │  Scenario Editor            │
│  ┌─────────────────────────┐  │  [Run] [Stop]              │
│  │ Name        │ Value     │  │  Add Steps:                │
│  │ Temperature │ 25        │  │  Wait: [Command] [Reply]  │
│  │ Pressure    │ 101.3     │  │  Send: [Response] [Cmd]   │
│  └─────────────────────────┘  │  ─────────────────────────  │
│  +───────────────────────+   │  [Step List]               │
│  │ Parsed from Commands   │   │  1. SetVariable State=RDY │
│  └───────────────────────+   │  2. WaitCommand LOGIN      │
│                               │  3. SendResponse LOGIN_OK │
├───────────────────────────────┴─────────────────────────────┤
│  Manual Send                                                 │
│  [Command Input...] [Send] [Clear]                          │
├─────────────────────────────────────────────────────────────┤
│  Communication Log                                           │
│  [12:30:45.123] [TX] Message: HELLO                         │
│  [12:30:45.456] [RX] Message: HELLO_ACK                     │
└─────────────────────────────────────────────────────────────┘
```

---

## Connection Settings

### Server Mode
- Listens on the specified IP and port
- Waits for clients to connect
- Good for simulating devices

### Client Mode
- Connects to the specified server
- Good for testing server applications

### Controls
| Control | Description |
|---------|-------------|
| Mode | Dropdown to select Server or Client |
| IP Address | Target IP (127.0.0.1 for local) |
| Port | Port number (default: 9000) |
| Start | Begin listening/connecting |
| Stop | Disconnect |

---

## Variables Panel

Variables store state that can be:
- Set manually
- Parsed from received commands
- Used in messages with `${variableName}` syntax

### Manual Variables
- **Add**: Enter name and value, click Add
- **Edit**: Double-click a row to modify
- **Delete**: Select row and press Delete key
- **Reset**: Clear all variables

### Parsed Variables
Automatically extracted from commands using patterns defined in the Protocol.

---

## Scenario Editor

### Step Types

| Step Type | Description |
|-----------|-------------|
| **SetVariable** | Set a variable to a specific value |
| **WaitCommand** | Wait for a specific command from the client |
| **WaitResponse** | Wait for a response matching a pattern |
| **SendResponse** | Send a predefined response |
| **SendCommand** | Send a command to the client |
| **AutoReply** | Auto-respond to matching commands |
| **Delay** | Pause for specified milliseconds |
| **IfElse** | Conditional branching |
| **Label** | Mark a position for Goto |
| **Goto** | Jump to a labeled position |

### Creating Steps

1. Click the appropriate button in the "Add Steps" section
2. The step is added to the list
3. Select it to edit its properties

### Editing Steps

Select a step from the list to see its properties:

**SetVariable**
- Variable Name: Name of the variable
- Value: Value to set

**WaitCommand / WaitResponse**
- Command Name: Expected command
- Timeout (ms): How long to wait
- Goto On Success/Timeout: Jump to label on result

**SendResponse**
- Command Name: Response type
- Response: Message template (supports `${variables}`)

**SendCommand**
- Command Name: Command to send
- Payload: Message content

**AutoReply**
- Command Pattern: Pattern to match (use `*` for wildcard)
- Response: Auto-response template

**Delay**
- Duration (ms): Wait time

**IfElse**
- Condition: Expression (e.g., `${Temperature} >= 100`)
- Goto If True/If False: Labels to jump to

**Label**
- Label Name: Unique identifier for Goto

**Goto**
- Target Label: Label to jump to

### Reordering Steps

- Select a step
- Click ↑ or ↓ buttons to move
- Or drag and drop

---

## Protocol Definition

Protocols define the command/response format for parsing variables.

### Creating a Protocol

1. Click **Protocol → New Protocol**
2. Add Commands with templates:
   ```
   CommandName: STATUS
   ResponseTemplate: STATUS state=${State} temp=${Temperature}
   ```
3. Variables in `${variableName}` format are automatically parsed

### Loading a Protocol

- Use **Protocol → Open Protocol** to load a `.json` file
- Use **Protocol → Open Sample** for examples

---

## Manual Send

Send arbitrary commands without running a scenario.

1. Type or select a command from suggestions
2. Click **Send**
3. View responses in the Communication Log

**Clear**: Clears the command history

---

## Communication Log

Displays all socket communications:

- **[TX]**: Data sent
- **[RX]**: Data received
- **[Info]**: Status messages
- **[Debug]**: Debug information
- **[Error]**: Error messages

### Controls
- **Save**: Save log to file
- **Clear**: Clear the log display
- **Enable Log File**: Write to disk

---

## Running a Scenario

1. **Configure Connection**: Set up IP/Port and click Start
2. **Load or Create Scenario**: Design your simulation
3. **Click Run**: Begin scenario execution
4. **Monitor**: Watch the log for activity
5. **Click Stop**: End the scenario

### Execution Flow

1. Steps execute in order
2. `WaitCommand` pauses until expected command received
3. `IfElse` evaluates conditions and branches
4. `Goto` jumps to labeled positions
5. `Delay` creates pauses
6. `AutoReply` handles matching commands automatically

---

## Sample Projects

### Server - Temperature Device
Simulates a temperature measurement device:
- Waits for SET_PARAMS and START
- Polls temperature until target reached
- Reports status via GET_STATUS

### Client - Measurement Controller
Simulates a measurement controller:
- Sends SET_PARAMS and START
- Polls for measurement completion
- Handles results

### Load Sample
1. **File → Open Sample → Server - Temperature Device**
2. Start connection (Server mode, port 9000)
3. Click Run
4. Connect with a client application

---

## Tips and Best Practices

### Variable Interpolation
Use `${variableName}` in responses to insert variable values:
```
Response: STATUS state=${State} temp=${Temperature}
```

### Condition Syntax
Use expressions in IfElse conditions:
```
${Temperature} >= 100
${Status} == "COMPLETED"
${Count} < 10
```

### Timeout Handling
Always set appropriate timeouts for Wait steps:
- Network commands: 5000-10000ms
- Long operations: 60000ms or more

### Goto/Loop Patterns
Create loops using Labels and Goto:
```
Label: LoopStart
  ... do something ...
IfElse: ${Count} < 10
  Goto If True: LoopStart
```

### AutoReply Usage
Use AutoReply for frequently polled commands:
```
AutoReply: GET_STATUS
  Response: STATUS state=${State} temp=${Temperature}
```

---

## Keyboard Shortcuts

| Shortcut | Action |
|----------|--------|
| Ctrl+N | New Project |
| Ctrl+O | Open Project |
| Ctrl+S | Save Project |
| Delete | Remove selected step |
| ↑/↓ | Move step up/down |

---

## Troubleshooting

### Connection Fails
- Verify IP and port are correct
- Check firewall settings
- Ensure no other application is using the port

### Scenario Stops Unexpectedly
- Check timeout settings
- Review the log for error messages
- Verify step order is correct

### Variables Not Interpolating
- Ensure variable names match exactly (case-sensitive)
- Check variable is set before being used

### Goto Labels Not Working
- Labels must have unique names
- Verify label names match exactly

---

## File Formats

### Project File (.simproj)
JSON file containing:
- Project name
- Scenario definition
- Connection settings
- Protocol reference

### Protocol File (.json)
JSON file containing:
- Command definitions
- Response templates with variable placeholders

---

## Support

For issues and questions, please refer to the project repository.

---

*Document Version: 1.0*
