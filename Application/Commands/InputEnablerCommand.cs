using PtzJoystickControl.Core.Commands;
using PtzJoystickControl.Core.Devices;
using PtzJoystickControl.Core.Model;

namespace PtzJoystickControl.Application.Commands;

public class InputEnablerCommand : ICommand
{
    private readonly IGamepad _gamepad;

    public InputEnablerCommand(IGamepad gamepad)
    {
        _gamepad = gamepad;
    }

    public const string CommandNameString = "Input Enabler";
    public       string CommandName => CommandNameString;

    public string AxisParameterName   => "Action";
    public string ButtonParameterName => "Action";

    public IEnumerable<CommandValueOption> Options
    {
        get
        {
            if (options != null)
                return options;

            options = new List<CommandValueOption>();
            int count = 0;
            EnabledInputs ??= new Dictionary<string, bool>();
            foreach (IInput gamepadInput in _gamepad.Inputs)
            {
                options.Add(
                    new CommandValueOption(
                        gamepadInput.Id,
                        count
                    )
                );
                EnabledInputs[gamepadInput.Id] = false;
                count++;
            }

            return options;
        }
    }

    private List<CommandValueOption>? options;

    public Dictionary<string, bool>? EnabledInputs { get; set; }

    public void Execute(CommandValueOption value, bool buttonPressed)
    {
        if (EnabledInputs != null)
            EnabledInputs[value.Name] = buttonPressed;
    }

    public void Execute(CommandValueOption value)
    {
        throw new NotImplementedException();
    }

    public void Execute(int value)
    {
        throw new NotImplementedException();
    }
}