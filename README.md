# Nurse Scheduling System (NurseScheduler Pro)

A modern, comprehensive solution for automated nurse scheduling using a Genetic Algorithm. This project combines a high-performance C# WPF frontend with a flexible Python-based optimization engine.

## 🚀 Key Features

- **Automated Scheduling**: Generate optimal monthly or weekly schedules with a single click.
- **Genetic Algorithm Optimization**: Uses advanced evolutionary techniques to minimize rule violations.
- **Customizable Rules**: Define complex constraints such as:
  - Minimum/Maximum shifts per nurse.
  - Mandatory rest periods.
  - Skill-based assignments.
  - Fair distribution of weekends and night shifts.
- **Interactive UI**: A clean, modern WPF interface for managing nurses, shifts, units, and rules.
- **Excel Export**: Export generated schedules directly to formatted Excel reports.
- **Real-time Progress**: Track the optimization process with live fitness and violation metrics.

## 🛠️ Project Structure

The project is split into two main components:

1.  **`NurseScheduler.UI` (C# .NET WPF)**:
    - The main user interface and data management layer.
    - Handles CRUD operations for nurses, units, and rules.
    - Manages the lifecycle of the optimization process.
    - Exports results to external formats.

2.  **`optimizer` (Python 3)**:
    - The core optimization engine.
    - Implements a Genetic Algorithm (`genetic_algorithm.py`) tailored for scheduling.
    - Communicates with the C# UI via a JSON-based stdin/stdout protocol.
    - Calculates fitness and validates constraints in real-time.

## 📋 Requirements

- **Frontend**: .NET 8.0/10.0 SDK.
- **Backend**: Python 3.9+ with the following libraries:
  - `numpy` (for mathematical operations)
  - `pandas` (if used for data handling)
  - (See `optimizer/requirements.txt` for the full list)

## ⚙️ How It Works

1.  The user defines nurses, their skills, and global scheduling rules in the WPF UI.
2.  Upon clicking "Create Schedule", the UI prepares a JSON payload containing all constraints and data.
3.  The C# application spawns the Python `optimizer/main.py` process, passing the JSON via `stdin`.
4.  The Python engine runs the Genetic Algorithm, sending periodic `PROGRESS` updates back to the UI.
5.  Once optimized, the final `RESULT` (the schedule) is sent back to the UI via `stdout` in JSON format.
6.  The UI displays the schedule and allows for manual adjustments or exporting to Excel.

## 🛠️ Setup and Installation

1.  **Clone the repository**:
    ```bash
    git clone https://github.com/4-yerime/nurse-schedule-project.git
    ```
2.  **Restore .NET Dependencies**:
    Open the solution file in Visual Studio or run:
    ```bash
    dotnet restore
    ```
3.  **Install Python Requirements**:
    ```bash
    pip install -r optimizer/requirements.txt
    ```
4.  **Run the Application**:
    Build and run the `NurseScheduler.UI` project from your IDE or CLI.

## 📄 License

This project is licensed under the MIT License - see the LICENSE file for details.
