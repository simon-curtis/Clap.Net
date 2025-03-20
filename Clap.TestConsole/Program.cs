using Clap.TestConsole;

var model = TestModel.Parse(args);
Console.WriteLine($"number: {model.InputFile}");
Console.WriteLine($"number: {model.NumberOfTimes}");