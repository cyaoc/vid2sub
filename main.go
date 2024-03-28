package main

import (
	"fmt"
	"io"
	"net/http"
	"os"
	"os/exec"
	"path/filepath"
	"sync"

	tea "github.com/charmbracelet/bubbletea"
	"github.com/cheggaaa/pb/v3"
	"github.com/klauspost/cpuid/v2"
)

func ensureDirExists(dirPath string) (bool, error) {
	if _, err := os.Stat(dirPath); os.IsNotExist(err) {
		err := os.MkdirAll(dirPath, 0755)
		if err != nil {
			return false, fmt.Errorf("failed to create directory %s: %v", dirPath, err)
		}
		return true, nil
	} else if err != nil {
		return false, fmt.Errorf("error checking directory %s: %v", dirPath, err)
	}
	return true, nil
}

func downloadFile(url, filename string, wg *sync.WaitGroup, ch chan<- string) {
	defer wg.Done()

	resp, err := http.Get(url)
	if err != nil {
		ch <- fmt.Sprintf("Failed to download %s: %v", url, err)
		return
	}
	defer resp.Body.Close()

	out, err := os.Create(filename)
	if err != nil {
		ch <- fmt.Sprintf("Failed to create %s: %v", filename, err)
		return
	}
	defer out.Close()

	totalSize := resp.ContentLength
	bar := pb.Full.Start64(totalSize)
	bar.Set(pb.Bytes, true)

	proxyReader := bar.NewProxyReader(resp.Body)

	_, err = io.Copy(out, proxyReader)
	if err != nil {
		ch <- fmt.Sprintf("Failed to write %s: %v", filename, err)
		return
	}
	bar.Finish()
	ch <- fmt.Sprintf("Downloaded %s to %s successfully", url, filename)
}

func checkAndDownload(model string, useOpenVINO bool) {
	modelsDir := filepath.Join(".", "models")
	if ok, err := ensureDirExists(modelsDir); !ok {
		fmt.Println("Error:", err)
	} else {
		fmt.Println("Directory ensured:", modelsDir)
		filesToDownload := map[string]string{
			fmt.Sprintf("ggml-%s.bin", model): fmt.Sprintf("%s/ggml-%s.bin", modelsDir, model),
		}

		if useOpenVINO {
			filesToDownload[fmt.Sprintf("ggml-%s-encoder-openvino.xml", model)] = fmt.Sprintf("%s/ggml-%s-encoder-openvino.xml", modelsDir, model)
			filesToDownload[fmt.Sprintf("ggml-%s-encoder-openvino.bin", model)] = fmt.Sprintf("%s/ggml-%s-encoder-openvino.bin", modelsDir, model)
		}

		var wg sync.WaitGroup
		ch := make(chan string, len(filesToDownload))

		for file, path := range filesToDownload {
			if _, err := os.Stat(path); os.IsNotExist(err) {
				wg.Add(1)
				url := fmt.Sprintf("https://huggingface.co/cyaoc/whisper-ggml/resolve/main/models/%s", file)
				go downloadFile(url, path, &wg, ch)
			} else {
				fmt.Printf("%s already exists\n", path)
			}
		}

		go func() {
			wg.Wait()
			close(ch)
		}()

		for msg := range ch {
			fmt.Println(msg)
		}
	}
}

func IsIntelCPU() bool {
	return cpuid.CPU.VendorID == cpuid.Intel
}

func GetRootDirectory() (string, error) {
	dir, err := os.Getwd()
	if err != nil {
		return "", err
	}
	return dir, nil
}

func runWhisper() {
	rootDir, _ := GetRootDirectory()
	intelOpenVinoDir := filepath.Join(rootDir, "bin", "openvino")
	openVinoDir := filepath.Join(intelOpenVinoDir, "runtime", "cmake")
	openVinoLibPaths := filepath.Join(intelOpenVinoDir, "runtime", "bin", "intel64", "Release") + ";" +
		filepath.Join(intelOpenVinoDir, "runtime", "bin", "intel64", "Debug")
	tbbDir := filepath.Join(intelOpenVinoDir, "runtime", "3rdparty", "tbb")
	if _, err := os.Stat(tbbDir); !os.IsNotExist(err) {
		var prefix string
		if _, err := os.Stat(filepath.Join(tbbDir, "redist", "intel64", "vc14")); !os.IsNotExist(err) {
			prefix = filepath.Join(tbbDir, "redist", "intel64", "vc14")
		} else if _, err := os.Stat(filepath.Join(tbbDir, "bin", "intel64", "vc14")); !os.IsNotExist(err) {
			prefix = filepath.Join(tbbDir, "bin", "intel64", "vc14")
		} else if _, err := os.Stat(filepath.Join(tbbDir, "bin")); !os.IsNotExist(err) {
			prefix = filepath.Join(tbbDir, "bin")
		}
		if prefix != "" {
			openVinoLibPaths = prefix + ";" + openVinoLibPaths
		}
		var tbbCmakeDir string
		if _, err := os.Stat(filepath.Join(tbbDir, "cmake")); !os.IsNotExist(err) {
			tbbCmakeDir = filepath.Join(tbbDir, "cmake")
		} else if _, err := os.Stat(filepath.Join(tbbDir, "lib", "cmake", "TBB")); !os.IsNotExist(err) {
			tbbCmakeDir = filepath.Join(tbbDir, "lib", "cmake", "TBB")
		} else if _, err := os.Stat(filepath.Join(tbbDir, "lib64", "cmake", "TBB")); !os.IsNotExist(err) {
			tbbCmakeDir = filepath.Join(tbbDir, "lib64", "cmake", "TBB")
		} else if _, err := os.Stat(filepath.Join(tbbDir, "lib", "cmake", "tbb")); !os.IsNotExist(err) {
			tbbCmakeDir = filepath.Join(tbbDir, "lib", "cmake", "tbb")
		}
		if tbbCmakeDir != "" {
			os.Setenv("TBB_DIR", tbbCmakeDir)
		}
	}

	os.Setenv("INTEL_OPENVINO_DIR", intelOpenVinoDir)
	os.Setenv("OpenVINO_DIR", openVinoDir)
	os.Setenv("OPENVINO_LIB_PATHS", openVinoLibPaths)
	os.Setenv("PATH", openVinoLibPaths+";"+os.Getenv("PATH"))

	fmt.Println("[setupvars] OpenVINO environment initialized")

	cmd := exec.Command(filepath.Join(intelOpenVinoDir, "main.exe"), "-h")
	cmd.Env = os.Environ()
	output, err := cmd.CombinedOutput()
	if err != nil {
		fmt.Printf("Error running main.exe: %v\n", err)
	}
	fmt.Printf("Output of main.exe:\n%s\n", output)
}

type model struct {
	step        int
	filePath    string
	priority    string
	language    string
	useOpenVINO bool
}

type filePathMsg string
type priorityMsg string
type languageMsg string
type useOpenVINOMsg bool

func main() {
	p := tea.NewProgram(initialModel())
	if _, err := p.Run(); err != nil {
		fmt.Printf("Error: %v", err)
		os.Exit(1)
	}
}

func initialModel() model {
	return model{}
}

func (m model) Init() tea.Cmd {
	return nil
}

func (m model) Update(msg tea.Msg) (tea.Model, tea.Cmd) {
	switch msg := msg.(type) {

	case tea.KeyMsg:
		switch msg.String() {
		case "ctrl+c", "q":
			return m, tea.Quit
		}

	case filePathMsg:
		m.filePath = string(msg)
		m.step++
		return m, nil

	case priorityMsg:
		m.priority = string(msg)
		m.step++
		return m, nil

	case languageMsg:
		m.language = string(msg)
		m.step++
		return m, nil

	case useOpenVINOMsg:
		m.useOpenVINO = bool(msg)
		m.step++
		return m, nil
	}

	switch m.step {
	case 0:
		return m, askForFilePath()
	case 1:
		return m, askForPriority()
	case 2:
		return m, askForLanguage()
	case 3:
		return m, askForOpenVINO()
	case 4:
		fmt.Println("Configuration Complete:")
		fmt.Printf("File Path: %s\n", m.filePath)
		fmt.Printf("Priority: %s\n", m.priority)
		fmt.Printf("Language: %s\n", m.language)
		fmt.Printf("Use OpenVINO: %t\n", m.useOpenVINO)
		return m, tea.Quit
	}

	return m, nil
}

func (m model) View() string {
	switch m.step {
	case 0:
		return "Enter file path or drag and drop the file:\n"
	case 1:
		return "Choose a priority: [Speed, Performance]\n"
	case 2:
		return "Enter language code (e.g., 'en', 'ja', 'zh') or leave blank for 'auto':\n"
	case 3:
		return "Enable OpenVINO acceleration? [y/n]:\n"
	default:
		return "Thank you!\n"
	}
}

// Dummy functions to simulate asking questions. In a real application, you would replace these with actual bubbletea commands to prompt the user for input.
func askForFilePath() tea.Cmd {
	return func() tea.Msg {
		return filePathMsg("/path/to/file.mp4")
	}
}

func askForPriority() tea.Cmd {
	return func() tea.Msg {
		return priorityMsg("Speed")
	}
}

func askForLanguage() tea.Cmd {
	return func() tea.Msg {
		return languageMsg("en")
	}
}

func askForOpenVINO() tea.Cmd {
	return func() tea.Msg {
		return useOpenVINOMsg(true)
	}
}
