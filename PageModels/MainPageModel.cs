using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MauiApp2.Models;
using MauiApp2.Services;
using System.Diagnostics;
using System.Globalization;
using System.Windows.Input;

namespace MauiApp2.PageModels;

public partial class MainPageModel : ObservableObject, IProjectTaskPageModel
{
    private bool _isNavigatedTo;
    private bool _dataLoaded;
    private readonly ProjectRepository _projectRepository;
    private readonly TaskRepository _taskRepository;
    private readonly CategoryRepository _categoryRepository;
    private readonly ModalErrorHandler _errorHandler;
    private readonly SeedDataService _seedDataService;
    private HiveMqMqttClient _mqttService;

    private readonly Random _random = new();

    [ObservableProperty]
    private List<CategoryChartData> _todoCategoryData = [];

    [ObservableProperty]
    private List<Brush> _todoCategoryColors = [];

    [ObservableProperty]
    private List<ProjectTask> _tasks = [];

    [ObservableProperty]
    private List<Project> _projects = [];

    [ObservableProperty]
    bool _isBusy;

    [ObservableProperty]
    bool _isReadOnly = true;

    [ObservableProperty]
    bool _isRefreshing;

    [ObservableProperty]
    private string _today = DateTime.Now.ToString("dddd, MMM d");

    [ObservableProperty]
    private double _temperature;

    [ObservableProperty]
    private double _humidity;

    [ObservableProperty]
    private bool _isLampOn;

    [ObservableProperty]
    private bool _isFanOn;

    [ObservableProperty]
    private string _mqttConnectionStatus = "🔴 Disconnected";

    [ObservableProperty]
    private bool _isConnecting;

    [ObservableProperty]
    private float _maxTemperature = 0;

    [ObservableProperty]
    private bool _isTempVisible = true;

    [ObservableProperty]
    private float _maxTempSet;

    // Tambahan properti untuk status manual
    [ObservableProperty]
    private bool _manualModeLamp;

    [ObservableProperty]
    private bool _manualModeFan;

    public bool HasCompletedTasks => Tasks?.Any(t => t.IsCompleted) ?? false;

    public MainPageModel(SeedDataService seedDataService, ProjectRepository projectRepository,
        TaskRepository taskRepository, CategoryRepository categoryRepository, ModalErrorHandler errorHandler, HiveMqMqttClient mqttService)
    {
        _projectRepository = projectRepository;
        _taskRepository = taskRepository;
        _categoryRepository = categoryRepository;
        _errorHandler = errorHandler;
        _seedDataService = seedDataService;
        _mqttService = mqttService;
        ConnectMqtt();
        _mqttService.ConnectionStateChanged += OnConnectionStateChanged;
        _mqttService.MessageReceived += OnMqttMessageReceived;
    }

    private async void ConnectMqtt()
    {
        if (_mqttService == null)
            return;

        if (_mqttService.MqttClient != null && _mqttService.MqttClient.IsConnected)
            return;

        try
        {
            IsConnecting = true;
            await _mqttService.ConnectAsync();
            await _mqttService.SubscribeAsync("iot/sensor");
            await _mqttService.SubscribeAsync("iot/sensor/set");
            // Tambahkan subscribe untuk topik kontrol manual
            await _mqttService.SubscribeAsync("iot/actuator/lamp");
            await _mqttService.SubscribeAsync("iot/actuator/fan");
            Console.WriteLine("✅ MQTT Connected and Subscribed.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ MQTT Connect failed: {ex.Message}");
            MqttConnectionStatus = "❌ Connect Failed!";
            await AppShell.Current.DisplayAlert("MQTT Error", "Unable to connect MQTT server.", "OK");
        }
        finally
        {
            IsConnecting = false;
        }
    }

    private void OnConnectionStateChanged(string status)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            switch (status)
            {
                case "Connected":
                    MqttConnectionStatus = "🔵 Connected";
                    IsConnecting = false;
                    await _mqttService.SubscribeAsync("iot/sensor");
                    await _mqttService.SubscribeAsync("iot/sensor/set");
                    await _mqttService.SubscribeAsync("iot/actuator/lamp");
                    await _mqttService.SubscribeAsync("iot/actuator/fan");
                    break;

                case "Connecting":
                    MqttConnectionStatus = "🟡 Connecting...";
                    IsConnecting = true;
                    break;

                case "Reconnecting":
                    MqttConnectionStatus = "🟠 Reconnecting...";
                    IsConnecting = true;
                    break;

                case "Disconnected":
                    MqttConnectionStatus = "🔴 Disconnected";
                    IsConnecting = false;
                    break;

                case "ConnectFailed":
                    MqttConnectionStatus = "❌ Connect Failed!";
                    IsConnecting = false;
                    await AppShell.Current.DisplayAlert("Connection Failed", "MQTT Broker is not reachable.", "OK");
                    break;
            }
        });
    }

    private void OnMqttMessageReceived(string topic, string payload)
    {
        Console.WriteLine($"🔥 MQTT Message - Topic: {topic}, Payload: {payload}");

        try
        {
            var sensorData = System.Text.Json.JsonSerializer.Deserialize<SensorData>(payload);

            if (sensorData != null)
            {
                Temperature = sensorData.SuhuLampu;
                Humidity = sensorData.Kelembapan;
                IsLampOn = sensorData.StatusLampu;
                IsFanOn = sensorData.StatusKipas;
                MaxTemperature = sensorData.Threshold;
                // Tambahan: Status mode manual
                ManualModeLamp = sensorData.ManualModeLamp;
                ManualModeFan = sensorData.ManualModeFan;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error parsing JSON: {ex.Message}");
        }
    }

    private async Task LoadData()
    {
        try
        {
            IsBusy = true;
            Projects = await _projectRepository.ListAsync();

            var chartData = new List<CategoryChartData>();
            var chartColors = new List<Brush>();

            var categories = await _categoryRepository.ListAsync();
            foreach (var category in categories)
            {
                chartColors.Add(category.ColorBrush);

                var ps = Projects.Where(p => p.CategoryID == category.ID).ToList();
                int tasksCount = ps.SelectMany(p => p.Tasks).Count();

                chartData.Add(new(category.Title, tasksCount));
            }

            TodoCategoryData = chartData;
            TodoCategoryColors = chartColors;
            Tasks = await _taskRepository.ListAsync();
        }
        finally
        {
            IsBusy = false;
            OnPropertyChanged(nameof(HasCompletedTasks));
        }
    }

    private async Task InitData(SeedDataService seedDataService)
    {
        bool isSeeded = Preferences.Default.ContainsKey("is_seeded");
        if (!isSeeded)
        {
            await seedDataService.LoadSeedDataAsync();
        }
        Preferences.Default.Set("is_seeded", true);
        await Refresh();
    }

    [RelayCommand]
    private async Task Refresh()
    {
        try
        {
            IsRefreshing = true;
            await LoadData();
        }
        catch (Exception e)
        {
            _errorHandler.HandleError(e);
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    [RelayCommand]
    private async Task editMaxTemp()
    {
        IsTempVisible = !IsTempVisible;
        if (!IsTempVisible)
        {
            Debug.WriteLine("IsTempVisible: " + IsTempVisible);
        }
        else
        {
            Debug.WriteLine("IsTempVisible: " + IsTempVisible);
            await _mqttService.PublishAsync("iot/sensor/set", MaxTempSet.ToString());
        }
    }

    // Command baru untuk kontrol manual
    [RelayCommand]
    private async Task ToggleLamp()
    {
        try
        {
            if (_mqttService == null || !_mqttService.IsConnected)
                return;

            string command = IsLampOn ? "OFF" : "ON";
            await _mqttService.PublishAsync("iot/actuator/lamp", command);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error toggling lamp: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task ToggleFan()
    {
        try
        {
            if (_mqttService == null || !_mqttService.IsConnected)
                return;

            string command = IsFanOn ? "OFF" : "ON";
            await _mqttService.PublishAsync("iot/actuator/fan", command);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error toggling fan: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task SetAutoMode()
    {
        try
        {
            if (_mqttService == null || !_mqttService.IsConnected)
                return;

            await _mqttService.PublishAsync("iot/actuator/lamp", "AUTO");
            await _mqttService.PublishAsync("iot/actuator/fan", "AUTO");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error setting auto mode: {ex.Message}");
        }
    }

    [RelayCommand]
    private void NavigatedTo() => _isNavigatedTo = true;

    [RelayCommand]
    private void NavigatedFrom() => _isNavigatedTo = false;

    [RelayCommand]
    private async Task Appearing()
    {
        if (!_dataLoaded)
        {
            await InitData(_seedDataService);
            _dataLoaded = true;
            await Refresh();
        }
        else if (!_isNavigatedTo)
        {
            await Refresh();
        }
    }

    [RelayCommand]
    private Task TaskCompleted(ProjectTask task)
    {
        OnPropertyChanged(nameof(HasCompletedTasks));
        return _taskRepository.SaveItemAsync(task);
    }

    [RelayCommand]
    private Task AddTask() => Shell.Current.GoToAsync($"task");

    [RelayCommand]
    private Task NavigateToProject(Project project) => Shell.Current.GoToAsync($"project?id={project.ID}");

    [RelayCommand]
    private Task NavigateToTask(ProjectTask task) => Shell.Current.GoToAsync($"task?id={task.ID}");

    [RelayCommand]
    private async Task CleanTasks()
    {
        var completedTasks = Tasks.Where(t => t.IsCompleted).ToList();
        foreach (var task in completedTasks)
        {
            await _taskRepository.DeleteItemAsync(task);
            Tasks.Remove(task);
        }

        OnPropertyChanged(nameof(HasCompletedTasks));
        Tasks = new(Tasks);
        await AppShell.DisplayToastAsync("All cleaned up!");
    }
}