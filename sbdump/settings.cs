﻿using System;
using System.IO;

namespace arookas {
	class CommandLineSettings {
		string mInput, mOutput;
		bool mOutputHeader, mOutputText, mOutputData, mOutputDynsym, mOutputBss;

		public string Input { get { return mInput; } }
		public string Output { get { return mOutput; } }
		public bool OutputHeader { get { return mOutputHeader; } }
		public bool OutputText { get { return mOutputText; } }
		public bool OutputData { get { return mOutputData; } }
		public bool OutputDynsym { get { return mOutputDynsym; } }
		public bool OutputBss { get { return mOutputBss; } }

		public CommandLineSettings(CommandLine cmd) {
			if (cmd == null) {
				throw new ArgumentNullException("cmd");
			}
			foreach (var param in cmd) {
				switch (param.Name) {
					case "-in": mInput = param[0]; continue;
					case "-out": mInput = param[0]; continue;
					case "-H": mOutputHeader = true; continue;
					case "-h": mOutputHeader = false; continue;
					case "-T": mOutputText = true; continue;
					case "-t": mOutputText = false; continue;
					case "-D": mOutputData = true; continue;
					case "-d": mOutputData = false; continue;
					case "-S": mOutputDynsym = true; continue;
					case "-s": mOutputDynsym = false; continue;
					case "-B": mOutputBss = true; continue;
					case "-b": mOutputBss = false; continue;
				}
			}
			if (mInput == null) {
				throw new Exception("Missing input file setting.");
			}
			if (mOutput == null) {
				mOutput = Path.ChangeExtension(mInput, ".txt");
			}
		}
	}
}