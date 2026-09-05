using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace UnicornOverlord;

internal static class ModLayoutSmokeTest
{
	public static void Run()
	{
		var window = new MainWindow();
		try
		{
			window.Show();
			var model = (ViewModel)window.DataContext!;
			model.WorkspaceIndex = 1;
			foreach (ModCategory category in model.ModCategories)
			{
				model.SelectedModCategory = category;
				Dispatcher.UIThread.RunJobs();
				window.UpdateLayout();
				int count = window.GetVisualDescendants().OfType<MissionEditorView>().Count(view => view.IsEffectivelyVisible);
				int expected = category.SourceName == "编队" ? 1 : 0;
				if (count != expected) throw new InvalidDataException($"{category.SourceName}：任务编队界面应显示 {expected} 次，实际 {count} 次。");
				int defaultGear = window.GetVisualDescendants().OfType<Expander>().Count(view => view.IsEffectivelyVisible && view.Header?.ToString() == "默认装备表（全职业共享）");
				if (defaultGear != (category.SourceName == "职业" ? 1 : 0)) throw new InvalidDataException($"{category.SourceName}：默认装备表出现在错误模块。");
				Console.WriteLine($"布局自检通过：{category.SourceName}，任务编队 {count} 次，默认装备入口 {defaultGear} 次。");
			}
		}
		finally { window.Close(); }
	}
}
