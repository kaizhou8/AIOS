namespace AIOS.Tools;

/// <summary>
/// 计算器工具 - 执行数学运算
/// </summary>
public class CalculatorTool : ITool
{
    public string Name => "calculator";
    public string Description => "Perform mathematical calculations";
    
    public IReadOnlyList<ToolParameter> Parameters => new[]
    {
        new ToolParameter
        {
            Name = "operation",
            Type = "string",
            Description = "Mathematical operation to perform",
            Required = true,
            AllowedValues = new[] { "add", "subtract", "multiply", "divide", "power", "sqrt", "sin", "cos", "tan" }
        },
        new ToolParameter
        {
            Name = "a",
            Type = "number",
            Description = "First operand",
            Required = false
        },
        new ToolParameter
        {
            Name = "b",
            Type = "number",
            Description = "Second operand",
            Required = false
        }
    };

    public Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters, CancellationToken cancellationToken = default)
    {
        try
        {
            var operation = parameters["operation"]?.ToString()?.ToLower();
            
            double result = operation switch
            {
                "add" => GetDouble(parameters, "a") + GetDouble(parameters, "b"),
                "subtract" => GetDouble(parameters, "a") - GetDouble(parameters, "b"),
                "multiply" => GetDouble(parameters, "a") * GetDouble(parameters, "b"),
                "divide" => GetDouble(parameters, "b") != 0 ? GetDouble(parameters, "a") / GetDouble(parameters, "b") : throw new DivideByZeroException(),
                "power" => Math.Pow(GetDouble(parameters, "a"), GetDouble(parameters, "b")),
                "sqrt" => Math.Sqrt(GetDouble(parameters, "a")),
                "sin" => Math.Sin(GetDouble(parameters, "a")),
                "cos" => Math.Cos(GetDouble(parameters, "a")),
                "tan" => Math.Tan(GetDouble(parameters, "a")),
                _ => throw new ArgumentException($"Unsupported operation: {operation}")
            };

            return Task.FromResult(new ToolResult
            {
                Success = true,
                Data = result
            });
        }
        catch (Exception ex)
        {
            return Task.FromResult(new ToolResult
            {
                Success = false,
                ErrorMessage = ex.Message
            });
        }
    }

    private static double GetDouble(Dictionary<string, object> parameters, string key)
    {
        if (!parameters.TryGetValue(key, out var value))
            throw new ArgumentException($"Missing parameter: {key}");

        return value switch
        {
            double d => d,
            int i => i,
            float f => f,
            string s => double.Parse(s),
            _ => throw new ArgumentException($"Invalid parameter type for {key}")
        };
    }
}
