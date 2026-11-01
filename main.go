// Single-click Windows GUI launcher for the real C#/.NET Framework WinForms application.
// The complete WinForms source is embedded into this EXE. No CMD, VBS, PowerShell,
// network access, external unpackers, or downloadable dependencies are used.
// The first run compiles the embedded C# into a cached WinForms EXE using the
// Windows .NET Framework compiler (already proven available in the user's setup).
// All compile/start errors are surfaced using a native Windows error message box.
package main

import (
	"crypto/sha256"
	"embed"
	"encoding/hex"
	"fmt"
	"io/fs"
	"os"
	"os/exec"
	"path/filepath"
	"runtime"
	"strings"
	"syscall"
	"unsafe"
)

//go:embed src/*.cs
var sources embed.FS

const appVersion = "0.5.3-beta.1"

func messageBox(title string, body string) {
	dll := syscall.NewLazyDLL("user32.dll")
	proc := dll.NewProc("MessageBoxW")
	titlePtr, _ := syscall.UTF16PtrFromString(title)
	bodyPtr, _ := syscall.UTF16PtrFromString(body)
	proc.Call(0, uintptr(unsafe.Pointer(bodyPtr)), uintptr(unsafe.Pointer(titlePtr)), 0x10)
	runtime.KeepAlive(titlePtr)
	runtime.KeepAlive(bodyPtr)
}

func run() error {
	dir := strings.TrimSpace(os.Getenv("LOCALAPPDATA"))
	if dir == "" {
		dir = os.TempDir()
	}
	base := filepath.Join(dir, "WoWRegionSwitcherWinForms", "managed_v0_5_3")
	srcDir := filepath.Join(base, "src")
	managedExe := filepath.Join(base, "WowRegionSwitcher_Managed.exe")
	buildLog := filepath.Join(base, "build.log")
	stampFile := filepath.Join(base, "source.sha256")

	if err := os.MkdirAll(srcDir, 0700); err != nil {
		return fmt.Errorf("创建应用目录失败：%v", err)
	}
	entries, err := fs.ReadDir(sources, "src")
	if err != nil {
		return fmt.Errorf("读取内置 C# 源码失败：%v", err)
	}
	var paths []string
	h := sha256.New()
	for _, entry := range entries {
		if entry.IsDir() || !strings.HasSuffix(entry.Name(), ".cs") {
			continue
		}
		content, err := sources.ReadFile("src/" + entry.Name())
		if err != nil {
			return fmt.Errorf("读取 %s 失败：%v", entry.Name(), err)
		}
		h.Write([]byte(entry.Name()))
		h.Write(content)
		out := filepath.Join(srcDir, entry.Name())
		paths = append(paths, out)
		if err := os.WriteFile(out, content, 0600); err != nil {
			return fmt.Errorf("提取源码失败 %s：%v", entry.Name(), err)
		}
	}
	hash := hex.EncodeToString(h.Sum(nil))
	currentHash, _ := os.ReadFile(stampFile)
	rebuild := string(currentHash) != hash
	if _, err := os.Stat(managedExe); err != nil {
		rebuild = true
	}

	if rebuild {
		winDir := os.Getenv("WINDIR")
		if winDir == "" {
			winDir = `C:\Windows`
		}
		csc := filepath.Join(winDir, "Microsoft.NET", "Framework64", "v4.0.30319", "csc.exe")
		if _, err := os.Stat(csc); err != nil {
			csc = filepath.Join(winDir, "Microsoft.NET", "Framework", "v4.0.30319", "csc.exe")
		}
		if _, err := os.Stat(csc); err != nil {
			return fmt.Errorf("本机未找到 .NET Framework 4.x 的 C# 编译器。\n已检查 Framework64 / Framework。\n需要启用 Windows .NET Framework 4.x。\n应用目录：%s", base)
		}
		args := []string{
			"/nologo", "/target:winexe", "/optimize+", "/platform:anycpu", "/codepage:65001",
			"/out:" + managedExe,
			"/reference:System.dll", "/reference:System.Core.dll",
			"/reference:System.Windows.Forms.dll", "/reference:System.Drawing.dll",
			"/reference:System.Xml.dll",
		}
		args = append(args, paths...)
		cmd := exec.Command(csc, args...)
		cmd.Dir = base
		cmd.SysProcAttr = &syscall.SysProcAttr{HideWindow: true}
		output, compileErr := cmd.CombinedOutput()
		os.WriteFile(buildLog, append([]byte("Version "+appVersion+"\r\n"), output...), 0600)
		if compileErr != nil {
			summary := strings.TrimSpace(string(output))
			if len(summary) > 3000 {
				summary = summary[:3000]
			}
			return fmt.Errorf("WinForms 首次初始化（C# 编译）失败：%v\n\n%s\n\n完整日志：%s", compileErr, summary, buildLog)
		}
		if _, err := os.Stat(managedExe); err != nil {
			return fmt.Errorf("编译器没有生成 WinForms EXE。\n日志：%s", buildLog)
		}
		if err := os.WriteFile(stampFile, []byte(hash), 0600); err != nil {
			return fmt.Errorf("写入软件版本标记失败：%v", err)
		}
	}
	cmd := exec.Command(managedExe, os.Args[1:]...)
	cmd.Dir = base
	if err := cmd.Start(); err != nil {
		return fmt.Errorf("启动 WinForms 主程序失败：%v\n\n文件：%s", err, managedExe)
	}
	// The managed program catches all UI/runtime exceptions and displays its own
	// detailed copyable error dialog. Keep this bootstrap resident until it exits.
	if err := cmd.Wait(); err != nil {
		return fmt.Errorf("WinForms 主程序异常退出：%v\n\n诊断日志：%s", err,
			filepath.Join(dir, "WoWRegionSwitcherWinForms", "launcher.log"))
	}
	return nil
}

func main() {
	defer func() {
		if recovered := recover(); recovered != nil {
			messageBox("魔兽双区服助手 - 启动错误", fmt.Sprintf("启动器发生意外错误：%v", recovered))
		}
	}()
	if err := run(); err != nil {
		messageBox("魔兽双区服助手 - 启动错误", err.Error())
	}
}
