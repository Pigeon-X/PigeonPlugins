using System;
using System.Collections.Generic;
using System.Linq;
using TShockAPI;

namespace VBY.Basic.Command;

public class SubCmdNodeList : SubCmdNode
{
	public List<SubCmdNode> SubCmds = new List<SubCmdNode>();

	public SubCmdNode this[string name]
	{
		get
		{
			SubCmdNode subCmdNode = this;
			if (name.Contains('.'))
			{
				string[] array = name.Split('.', StringSplitOptions.RemoveEmptyEntries);
				for (int i = 0; i < array.Length; i++)
				{
					subCmdNode = ((SubCmdNodeList)subCmdNode)[array[i]];
				}
			}
			else
			{
				subCmdNode = SubCmds.Find((SubCmdNode x) => x.CmdName == name);
				if (subCmdNode == null)
				{
					throw new Exception(FullCmdName + "'s SubCmd '" + name + "' not find");
				}
			}
			return subCmdNode;
		}
	}

	internal SubCmdNodeList(string cmdName, string description, params string[] names)
		: base(cmdName, description, names)
	{
		DescCmd = true;
		NodeType = NodeType.List;
	}

	public override void Run(CommandArgs args)
	{
		if (NoCanRun(args.Player))
		{
			return;
		}
		if (args.Parameters.Count == CmdIndex)
		{
			args.Player.SendInfoMessage("/" + args.Message.Trim() + " 的子命令");
			bool flag = false;
			foreach (SubCmdNode subCmd in SubCmds)
			{
				if (subCmd.Enabled)
				{
					args.Player.SendInfoMessage(subCmd.Names[0] + " " + subCmd.Description);
					flag = true;
				}
			}
			if (!flag)
			{
				args.Player.SendInfoMessage("好像没有可用子命令,问问腐竹是不是配错了");
			}
			return;
		}
		string findText = args.Parameters[CmdIndex];
		bool flag2 = false;
		foreach (SubCmdNode subCmd2 in SubCmds)
		{
			if (subCmd2.Enabled && subCmd2.Names.Any((string x) => string.Equals(x, findText, StringComparison.OrdinalIgnoreCase)))
			{
				subCmd2.Run(args);
				flag2 = true;
			}
		}
		if (!flag2)
		{
			args.Player.SendInfoMessage("未知参数 " + findText);
		}
	}

	internal override void OutCmdRun(CommandArgs args, int outCount)
	{
		if (NoCanRun(args.Player))
		{
			return;
		}
		if (args.Parameters.Count == CmdIndex - outCount)
		{
			args.Player.SendInfoMessage("/" + args.Message.Trim() + " 的子命令");
			bool flag = false;
			foreach (SubCmdNode subCmd in SubCmds)
			{
				if (subCmd.Enabled)
				{
					args.Player.SendInfoMessage(subCmd.Names[0] + " " + subCmd.Description);
					flag = true;
				}
			}
			if (!flag)
			{
				args.Player.SendInfoMessage("好像没有可用子命令,问问腐竹是不是配错了");
			}
			return;
		}
		string findText = args.Parameters[CmdIndex - outCount];
		bool flag2 = false;
		foreach (SubCmdNode subCmd2 in SubCmds)
		{
			if (subCmd2.Enabled && subCmd2.Names.Any((string x) => string.Equals(x, findText, StringComparison.OrdinalIgnoreCase)))
			{
				subCmd2.OutCmdRun(args, outCount);
				flag2 = true;
			}
		}
		if (!flag2)
		{
			args.Player.SendInfoMessage("未知参数 " + findText);
		}
	}

	internal void AddNode(SubCmdNode addNode)
	{
		addNode.CmdIndex = CmdIndex + 1;
		addNode.FullCmdName = FullCmdName + "." + addNode.CmdName;
		addNode.Parent = this;
		if (addNode.NodeType == NodeType.Run)
		{
			((SubCmdNodeRun)addNode).MinArgsCount += addNode.CmdIndex;
		}
		SubCmds.Add(addNode);
	}

	public SubCmdNodeList AddList(string cmdName, string description, params string[] names)
	{
		SubCmdNodeList subCmdNodeList = new SubCmdNodeList(cmdName, description, names);
		AddNode(subCmdNodeList);
		return subCmdNodeList;
	}

	public SubCmdNodeList AddList(string cmdName, string description)
	{
		return AddList(cmdName, description, cmdName.ToLower());
	}

	public SubCmdNodeRun AddCmd(SubCmdD runCmd, string cmdName, string description, string[] names, string? argsHelpText = null, string? helpText = null, int minArgsCount = 0)
	{
		SubCmdNodeRun subCmdNodeRun = new SubCmdNodeRun(runCmd, cmdName, description, names, argsHelpText, helpText, minArgsCount);
		AddNode(subCmdNodeRun);
		return subCmdNodeRun;
	}

	public SubCmdNodeRun AddCmd(SubCmdD runCmd, string cmdName, string description, string? argsHelpText = null, string? helpText = null, int minArgsCount = 0)
	{
		return AddCmd(runCmd, cmdName, description, new string[1] { cmdName.ToLower() }, argsHelpText, helpText, minArgsCount);
	}

	public SubCmdNodeRun AddCmdA(SubCmdD runCmd, string cmdName, string description, string[] names, string argsHelpText, string? helpText = null)
	{
		SubCmdNodeRun subCmdNodeRun = new SubCmdNodeRun(runCmd, cmdName, description, names, argsHelpText, helpText, argsHelpText.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length);
		AddNode(subCmdNodeRun);
		return subCmdNodeRun;
	}

	public SubCmdNodeRun AddCmdA(SubCmdD runCmd, string cmdName, string description, string argsHelpText, string? helpText = null)
	{
		return AddCmdA(runCmd, cmdName, description, new string[1] { cmdName.ToLower() }, argsHelpText, helpText);
	}

	public SubCmdNodeRun AddCmdAA(SubCmdD runCmd, string description, string argsHelpText, string? helpText = null)
	{
		return AddCmdA(runCmd, runCmd.Method.Name.LastWord(), description, new string[1] { runCmd.Method.Name.LastWord().ToLower() }, argsHelpText, helpText);
	}

	public void SetAllNodeRun(AllowInfo info)
	{
		foreach (SubCmdNode subCmd in SubCmds)
		{
			if (subCmd.NodeType == NodeType.List)
			{
				((SubCmdNodeList)subCmd).SetAllNodeRun(info);
			}
			else if (subCmd.NodeType == NodeType.Run)
			{
				subCmd.SetAllowInfo(info);
			}
		}
	}
}
