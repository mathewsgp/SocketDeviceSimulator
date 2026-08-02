using System;
using System.IO;
using Newtonsoft.Json;
using SocketSimulator.Models;

namespace SocketSimulator.Services
{
    public class ProjectService
    {
        private static ProjectService? _instance;
        public static ProjectService Instance => _instance ??= new ProjectService();

        private Project? _currentProject;
        private readonly LoggingService _logger = LoggingService.Instance;
        
        private static readonly JsonSerializerSettings _serializerSettings = new JsonSerializerSettings
        {
            Converters = new List<JsonConverter> { new ScenarioStepConverter() },
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore
        };

        public Project? CurrentProject => _currentProject;

        public event EventHandler? ProjectChanged;
        public event EventHandler? ProjectDirtyChanged;

        private ProjectService() { }

        public Project CreateNew()
        {
            _currentProject = new Project
            {
                Name = "Untitled Project",
                FilePath = string.Empty,
                IsDirty = false
            };
            _logger.Info("Project", "Created new project");
            OnProjectChanged();
            return _currentProject;
        }

        public Project? Load(string filePath)
        {
            try
            {
                var json = File.ReadAllText(filePath);
                var projectData = JsonConvert.DeserializeObject<ProjectData>(json, _serializerSettings);
                
                if (projectData != null)
                {
                    _currentProject = new Project
                    {
                        Name = projectData.Name,
                        FilePath = filePath,
                        IsDirty = false
                    };

                    // Load protocol if specified
                    if (!string.IsNullOrEmpty(projectData.ProtocolFile))
                    {
                        _currentProject.Protocol = ProtocolService.Instance.LoadProtocol(projectData.ProtocolFile);
                    }

                    // Load scenario
                    if (projectData.Scenario != null)
                    {
                        _currentProject.Scenario = projectData.Scenario;
                    }

                    // Load connection settings
                    if (projectData.ConnectionSettings != null)
                    {
                        _currentProject.ConnectionSettings = projectData.ConnectionSettings;
                    }

                    _logger.Info("Project", $"Loaded project: {filePath}");
                    OnProjectChanged();
                    return _currentProject;
                }
            }
            catch (Exception ex)
            {
                _logger.Error("Project", $"Failed to load project: {ex.Message}");
            }
            return null;
        }

        public bool Save(string? filePath = null)
        {
            if (_currentProject == null)
                return false;

            try
            {
                var path = filePath ?? _currentProject.FilePath;
                if (string.IsNullOrEmpty(path))
                    return false;

                var projectData = new ProjectData
                {
                    Name = _currentProject.Name,
                    ProtocolFile = string.Empty, // Protocol saved separately
                    Scenario = _currentProject.Scenario,
                    ConnectionSettings = _currentProject.ConnectionSettings
                };

                var json = JsonConvert.SerializeObject(projectData, _serializerSettings);
                File.WriteAllText(path, json);

                _currentProject.FilePath = path;
                _currentProject.IsDirty = false;
                _logger.Info("Project", $"Saved project: {path}");
                OnProjectDirtyChanged();
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error("Project", $"Failed to save project: {ex.Message}");
                return false;
            }
        }

        public void MarkDirty()
        {
            if (_currentProject != null)
            {
                _currentProject.IsDirty = true;
                OnProjectDirtyChanged();
            }
        }

        private void OnProjectChanged()
        {
            ProjectChanged?.Invoke(this, EventArgs.Empty);
        }

        private void OnProjectDirtyChanged()
        {
            ProjectDirtyChanged?.Invoke(this, EventArgs.Empty);
        }

        private class ProjectData
        {
            public string Name { get; set; } = string.Empty;
            public string ProtocolFile { get; set; } = string.Empty;
            public Scenario? Scenario { get; set; }
            public ConnectionSettings? ConnectionSettings { get; set; }
        }
    }
}
