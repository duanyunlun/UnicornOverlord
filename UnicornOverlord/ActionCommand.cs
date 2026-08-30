using System.Windows.Input;

namespace UnicornOverlord
{
	internal class ActionCommand : ICommand
	{
		public event EventHandler? CanExecuteChanged { add { } remove { } }
		private readonly Action<object?> mAction;

		public ActionCommand(Action<object?> action) => mAction = action;

		public bool CanExecute(object? parameter) => true;

		public void Execute(object? parameter) => mAction(parameter);
	}
}
