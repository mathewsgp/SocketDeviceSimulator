using System;
using System.Windows.Input;
using Microsoft.Win32;
using SocketSimulator.Commands;
using SocketSimulator.Models;
using SocketSimulator.Services;

namespace SocketSimulator.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private readonly ProjectService _projectService;
        private readonly LoggingService _logger;

        private string _windowTitle = "Socket Simulator";

        public ConnectionViewModel ConnectionVM { get; }
        public ProtocolViewModel ProtocolVM { get; }
        public ScenarioViewModel ScenarioVM { get; }
        public VariableViewModel VariableVM { get; }
        public LogViewModel LogVM { get; }
        public ConsoleViewModel ConsoleVM { get; }

        public string WindowTitle
        {
            get => _windowTitle;
            set => SetProperty(ref _windowTitle, value);
        }

        public string Title
        {
            get
            {
                var project = _projectService.CurrentProject;
                var dirty = project?.IsDirty == true ? "*" : "";
                return $"Socket Simulator{dirty} - {project?.Name ?? "No Project"}";
            }
        }

        public ICommand NewProjectCommand { get; }
        public ICommand OpenProjectCommand { get; }
        public ICommand SaveProjectCommand { get; }
        public ICommand SaveProjectAsCommand { get; }
        public ICommand ExitCommand { get; }
        public ICommand AboutCommand { get; }

        public MainViewModel()
        {
            _projectService = ProjectService.Instance;
            _logger = LoggingService.Instance;

            // Initialize ViewModels
            ConnectionVM = new ConnectionViewModel();
            ProtocolVM = new ProtocolViewModel();
            ScenarioVM = new ScenarioViewModel();
            VariableVM = new VariableViewModel();
            LogVM = new LogViewModel();
            ConsoleVM = new ConsoleViewModel();

            // Commands
            NewProjectCommand = new RelayCommand(NewProject);
            OpenProjectCommand = new RelayCommand(OpenProject);
            SaveProjectCommand = new RelayCommand(SaveProject);
            SaveProjectAsCommand = new RelayCommand(SaveProjectAs);
            ExitCommand = new RelayCommand(Exit);
            AboutCommand = new RelayCommand(About);

            // Subscribe to project events
            _projectService.ProjectChanged += OnProjectChanged;
            _projectService.ProjectDirtyChanged += OnProjectDirtyChanged;

            // Create a new project on startup
            NewProject();
            _logger.Info("App", "Application started");
        }

        private void OnProjectChanged(object? sender, EventArgs e)
        {
            OnPropertyChanged(nameof(Title));
            
            var project = _projectService.CurrentProject;
            if (project != null)
            {
                ConnectionVM.ApplySettings(project.ConnectionSettings);
                if (project.Scenario != null)
                {
                    ScenarioVM.LoadScenario(project.Scenario);
                }
            }
        }

        private void OnProjectDirtyChanged(object? sender, EventArgs e)
        {
            OnPropertyChanged(nameof(Title));
        }

        private void NewProject()
        {
            var project = _projectService.CreateNew();
            ConnectionVM.ApplySettings(project.ConnectionSettings);
            ScenarioVM.NewScenario();
            OnPropertyChanged(nameof(Title));
            _logger.Info("Project", "New project created");
        }

        private void OpenProject()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Socket Simulator Project (*.simproj)|*.simproj|All Files (*.*)|*.*",
                Title = "Open Project"
            };

            if (dialog.ShowDialog() == true)
            {
                var project = _projectService.Load(dialog.FileName);
                if (project != null)
                {
                    ConnectionVM.ApplySettings(project.ConnectionSettings);
                    if (project.Scenario != null)
                    {
                        ScenarioVM.LoadScenario(project.Scenario);
                    }
                    OnPropertyChanged(nameof(Title));
                    _logger.Info("Project", $"Opened project: {dialog.FileName}");
                }
            }
        }

        private void SaveProject()
        {
            var project = _projectService.CurrentProject;
            if (project == null) return;

            if (string.IsNullOrEmpty(project.FilePath))
            {
                SaveProjectAs();
                return;
            }

            UpdateProjectFromViewModels();
            _projectService.Save();
            OnPropertyChanged(nameof(Title));
        }

        private void SaveProjectAs()
        {
            var dialog = new SaveFileDialog
            {
                Filter = "Socket Simulator Project (*.simproj)|*.simproj|All Files (*.*)|*.*",
                Title = "Save Project As"
            };

            if (dialog.ShowDialog() == true)
            {
                UpdateProjectFromViewModels();
                _projectService.Save(dialog.FileName);
                OnPropertyChanged(nameof(Title));
                _logger.Info("Project", $"Saved project as: {dialog.FileName}");
            }
        }

        private void UpdateProjectFromViewModels()
        {
            var project = _projectService.CurrentProject;
            if (project == null) return;

            project.ConnectionSettings = ConnectionVM.GetSettings();
            project.Scenario = ScenarioVM.GetScenario();
        }

        private void Exit()
        {
            System.Windows.Application.Current.Shutdown();
        }

        private void About()
        {
            System.Windows.MessageBox.Show(
                "Socket Simulator v1.0\n\n" +
                "A TCP/IP command/response protocol simulator\n" +
                "for developers and testers.\n\n" +
                "Features:\n" +
                "- TCP Server/Client mode\n" +
                "- JSON protocol definitions\n" +
                "- Variable substitution\n" +
                "- Scenario scripting\n" +
                "- Real-time communication log",
                "About Socket Simulator",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
        }
    }
}
