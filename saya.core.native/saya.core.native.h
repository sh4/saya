// saya.core.native.h

#pragma once
#pragma comment(lib, "advapi32.lib")
#pragma comment(lib, "shell32.lib")

using namespace System;

namespace
{
	class ProcessInfoImpl;
}

namespace saya
{
namespace core
{
namespace native
{

public ref class ProcessInfo
{
	ProcessInfoImpl* m_impl;
public:
	ProcessInfo();
	~ProcessInfo();
	!ProcessInfo();

	String^ GetProcessCommandLine(int processId);
	String^ GetProcessArguments(int processId);
};

public ref class ShellIconInfo
{
	SHFILEINFO* shInfo;
public:
	ShellIconInfo() : shInfo(new SHFILEINFO)
	{

	}

	~ShellIconInfo()
	{
		this->!ShellIconInfo();
	}

	!ShellIconInfo()
	{
		if (shInfo)
		{
			delete shInfo;
			shInfo = nullptr;
		}
	}

	IntPtr GetIcon(String^ path)
	{
		pin_ptr<wchar_t> pathPtr = &(path->ToCharArray())[0];
		if (!SHGetFileInfo(pathPtr, 0, shInfo, sizeof(SHFILEINFO), SHGFI_ICON | SHGFI_LARGEICON))
		{
			return IntPtr::Zero;
		}
		// hIcon ‚ÍŒÄ‚Ño‚µŒ³‚ÅŠJ•ú‚·‚é‚±‚Æ
		return IntPtr(shInfo->hIcon);
	}
};

} // native
} // core
} // saya

