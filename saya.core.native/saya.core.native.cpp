// これは メイン DLL ファイルです。

#include "stdafx.h"
#include "saya.core.native.h"

namespace
{
	class Handle
	{
		HANDLE m_handle;
	public:
		Handle(HANDLE handle) : m_handle(handle) {}
		~Handle()
		{ 
			CloseHandle(m_handle);
			m_handle = INVALID_HANDLE_VALUE;
		}

		HANDLE GetHandle() const { return m_handle; }
	};

	template <typename T>
	class ProcAddress
	{
		HMODULE m_module;
		T* m_proc;
	public:
		ProcAddress(LPCWSTR libraryName, LPCSTR procName) :
			m_module(LoadLibrary(libraryName)),
			m_proc(reinterpret_cast<T*>(GetProcAddress(m_module, procName)))
		{
		}

		~ProcAddress()
		{
			FreeLibrary(m_module);
			m_module = nullptr;
		}

		T* GetProc() const { return m_proc; }
	};

	bool PrepareCurrentProcessPrivileges()
	{
		HANDLE tokenNativeHandle;
		if (!OpenProcessToken(GetCurrentProcess(), TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY, &tokenNativeHandle))
		{
			return false;
		}

		Handle token(tokenNativeHandle);

		TOKEN_PRIVILEGES tokenp = {};
		if (!LookupPrivilegeValue(nullptr, SE_DEBUG_NAME, &tokenp.Privileges[0].Luid))
		{
			return false;
		}
		tokenp.PrivilegeCount = 1;
		tokenp.Privileges[0].Attributes = SE_PRIVILEGE_ENABLED;
		if (!AdjustTokenPrivileges(token.GetHandle(), FALSE, &tokenp, sizeof(TOKEN_PRIVILEGES), nullptr, 0))
		{
			return false;
		}

		return true;
	}

	HANDLE OpenProcessWithReadonly(int processId) {
		return OpenProcess(PROCESS_QUERY_INFORMATION | PROCESS_VM_READ, FALSE, processId);
	}

	class ProcessParameter {
		std::wstring m_commandLine;
		std::wstring m_imagePathName;
	public:
		ProcessParameter() {
		}
		ProcessParameter(const std::vector<wchar_t>& commandLine, const std::vector<wchar_t>& imagePathName) :
			m_commandLine(&commandLine[0]),
			m_imagePathName(&imagePathName[0])
		{
		}

		const std::wstring& CommandLine() const
		{
			return m_commandLine;
		}

		const std::wstring& ImagePathName() const
		{
			return m_imagePathName;
		}
	};

	class ProcessInfoImpl {
		ProcAddress<decltype(NtQueryInformationProcess)> m_proc;
	public:
		ProcessInfoImpl() : m_proc(TEXT("ntdll.dll"), "NtQueryInformationProcess")
		{
			if (!PrepareCurrentProcessPrivileges())
			{
				throw gcnew System::ComponentModel::Win32Exception();
			}
		}

		~ProcessInfoImpl()
		{
		}

		ProcessParameter GetProcessParameter(const Handle& process)
		{
			PPEB pebBaseAddress = GetPebBaseAddress(process);
			if (!pebBaseAddress)
			{
				return ProcessParameter();
			}

			PEB peb;
			if (!ReadProcessMemory(process.GetHandle(), pebBaseAddress, &peb, sizeof(peb), nullptr))
			{
				return ProcessParameter();
			}

			RTL_USER_PROCESS_PARAMETERS pp;
			if (!ReadProcessMemory(process.GetHandle(), peb.ProcessParameters, &pp, sizeof(pp), nullptr))
			{
				return ProcessParameter();
			}

			std::vector<wchar_t> commandLine(pp.CommandLine.MaximumLength);
			if (!ReadProcessMemory(process.GetHandle(), pp.CommandLine.Buffer, &commandLine[0], pp.CommandLine.MaximumLength, nullptr))
			{
				return ProcessParameter();
			}

			std::vector<wchar_t> imagePathName(pp.ImagePathName.MaximumLength);
			if (!ReadProcessMemory(process.GetHandle(), pp.ImagePathName.Buffer, &imagePathName[0], pp.ImagePathName.MaximumLength, nullptr))
			{
				return ProcessParameter();
			}

			return std::move(ProcessParameter(commandLine, imagePathName));
		}
	private:
		PPEB GetPebBaseAddress(const Handle& process) const
		{
			PROCESS_BASIC_INFORMATION pbi;

			DWORD needed = 0;
			NTSTATUS status = m_proc.GetProc()(process.GetHandle(), ProcessBasicInformation, &pbi, sizeof(pbi), &needed);

			if (NT_SUCCESS(status) && pbi.PebBaseAddress)
			{
				return pbi.PebBaseAddress;
			}
			else
			{
				return nullptr;
			}
		}
	};
}

namespace saya
{
namespace core
{
namespace native
{

ProcessInfo::ProcessInfo() : m_impl(new ProcessInfoImpl())
{
}

ProcessInfo::~ProcessInfo()
{
	this->!ProcessInfo();
}

ProcessInfo::!ProcessInfo()
{
	if (m_impl)
	{
		delete m_impl;
		m_impl = nullptr;
	}
}

String^ ProcessInfo::GetProcessCommandLine(int processId)
{
	Handle process(OpenProcessWithReadonly(processId));
	ProcessParameter param = m_impl->GetProcessParameter(process);
	auto& cmdline(param.CommandLine());
	if (cmdline.size() > 0)
	{
		return gcnew String(cmdline.c_str(), 0, static_cast<int>(cmdline.size()));
	}
	else
	{
		return String::Empty;
	}
}

String^ ProcessInfo::GetProcessArguments(int processId)
{
	Handle process(OpenProcessWithReadonly(processId));
	ProcessParameter param = m_impl->GetProcessParameter(process);
	auto& cmdline(param.CommandLine());
	int imagePathNameLength = static_cast<int>(param.ImagePathName().size());
	int start = imagePathNameLength +(cmdline[0] == L'"' ? 1 + 1 : 0);
	int length = static_cast<int>(cmdline.size() - start);
	if (length > 0)
	{
		for (; cmdline[start] == L' ' && length > 0; start++, length--) {}
		return gcnew String(cmdline.c_str(), start, static_cast<int>(length));
	}
	else
	{
		return String::Empty;
	}
}

} // native
} // core
} // saya