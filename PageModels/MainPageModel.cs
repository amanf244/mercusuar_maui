using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MauiApp2.Models;
using MauiApp2.Services;
using System.Globalization; // Tambahan kalau perlu
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
    private  HiveMqMqttClient _mqttService;

    private readonly Random _random = new(); // Untuk generate data sensor random

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
    bool _isRefreshing;

    [ObservableProperty]
    private string _today = DateTime.Now.ToString("dddd, MMM d");

    // 🆕 Tambahan properti untuk sensor
    [ObservableProperty]
    private double _temperature;

    [ObservableProperty]
    private double _humidity;

    [ObservableProperty]
    private bool _isLampOn;

    [ObservableProperty]
    private bool _isFanOn;

    public bool HasCompletedTasks
        => Tasks?.Any(t => t.IsCompleted) ?? false;

    public MainPageModel(SeedDataService seedDataService, ProjectRepository projectRepository,
        TaskRepository taskRepository, CategoryRepository categoryRepository, ModalErrorHandler errorHandler, HiveMqMqttClient mqttService)
    {
        _projectRepository = projectRepository;
        _taskRepository = taskRepository;
        _categoryRepository = categoryRepository;
        _errorHandler = errorHandler;
        _seedDataService = seedDataService;
        _mqttService = mqttService;
        _mqttService.MessageReceived += OnMqttMessageReceived;
    }

    private void OnMqttMessageReceived(string topic, string payload)
    {
        // Ini dipanggil setiap ada data masuk
        Console.WriteLine($"🔥 MQTT Message - Topic: {topic}, Payload: {payload}");

        try
        {
            var sensorData = System.Text.Json.JsonSerializer.Deserialize<SensorData>(payload);

            if (sensorData != null)
            {
                //Dispatcher.Dispatch(() =>
                //{
                //    Temperature = sensorData.SuhuLampu;
                //    Humidity = sensorData.Kelembapan;
                //    IsLampOn = sensorData.StatusLampu;
                //    IsFanOn = sensorData.StatusKipas;

                //    // Kalau mau tampilkan semua dalam 1 label
                //    MqttMessage = $"Suhu: {Temperature}°C\nKelembapan: {Humidity}%\nLampu: {(IsLampOn ? "On" : "Off")}\nKipas: {(IsFanOn ? "On" : "Off")}";
                //});
                Temperature = sensorData.SuhuLampu;
                Humidity = sensorData.Kelembapan;
                IsLampOn = sensorData.StatusLampu;
                IsFanOn = sensorData.StatusKipas;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error parsing JSON: {ex.Message}");
        }
    }

    private async Task LoadData()
    {
        //var mqttService = new HiveMqMqttClient();
        //await mqttService.ConnectAsync();
        //await mqttService.PublishAsync("iot/sensor", "Hello MQTT");
        await _mqttService.ConnectAsync();
        await _mqttService.PublishAsync("iot/sensor", "Hello Aman");
        await _mqttService.SubscribeAsync("iot/sensor");

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

            // 🆕 Simulasikan data sensor setiap refresh
            SimulateSensorData();

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

    private void SimulateSensorData()
    {
        // 🔥 Update Temperature dan Humidity secara random
        /*Temperature = Math.Round(_random.NextDouble() * 20 + 20, 1);*/ // 20°C ~ 40°C
        Humidity = Math.Round(_random.NextDouble() * 50 + 30, 1);    // 30% ~ 80%

        // 🔥 Random nyalakan/matikan lampu dan kipas
        IsLampOn = _random.Next(2) == 0;
        IsFanOn = _random.Next(2) == 0;
    }

    [RelayCommand]
    private void NavigatedTo() =>
        _isNavigatedTo = true;

    [RelayCommand]
    private void NavigatedFrom() =>
        _isNavigatedTo = false;

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
    private Task AddTask()
        => Shell.Current.GoToAsync($"task");

    [RelayCommand]
    private Task NavigateToProject(Project project)
        => Shell.Current.GoToAsync($"project?id={project.ID}");

    [RelayCommand]
    private Task NavigateToTask(ProjectTask task)
        => Shell.Current.GoToAsync($"task?id={task.ID}");

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
