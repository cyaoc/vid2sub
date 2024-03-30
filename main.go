package main

import (
	"bufio"
	"fmt"
	"io"
	"net/http"
	"os"
	"os/exec"
	"path/filepath"
	"strings"
	"time"

	"github.com/charmbracelet/bubbles/key"
	inf "github.com/fzdwx/infinite"
	"github.com/fzdwx/infinite/components"
	"github.com/fzdwx/infinite/components/input/text"
	"github.com/fzdwx/infinite/components/progress"
	"github.com/fzdwx/infinite/components/selection/confirm"
	"github.com/fzdwx/infinite/components/selection/singleselect"
	"github.com/fzdwx/infinite/theme"
	"github.com/klauspost/cpuid/v2"
)

func ensureDirsExist(dirPaths ...string) error {
	for _, dirPath := range dirPaths {
		if _, err := os.Stat(dirPath); os.IsNotExist(err) {
			if err := os.MkdirAll(dirPath, 0755); err != nil {
				return fmt.Errorf("failed to create directory %s: %v", dirPath, err)
			}
		} else if err != nil {
			return fmt.Errorf("error checking directory %s: %v", dirPath, err)
		}
	}
	return nil
}

func isIntelCPU() bool {
	return cpuid.CPU.VendorID == cpuid.Intel
}

func GetRootDirectory(isRelease bool) (string, error) {
	if isRelease {
		exePath, err := os.Executable()
		if err != nil {
			return "", err
		}
		return filepath.Dir(exePath), nil
	}
	return os.Getwd()
}

func checkAndDownload(filesToDownload map[string]string) {
	var needToDownload []string
	for key, path := range filesToDownload {
		if _, err := os.Stat(path); os.IsNotExist(err) {
			needToDownload = append(needToDownload, key)
		}
	}
	if len(needToDownload) > 0 {
		progress.NewGroupWithCount(len(needToDownload)).AppendRunner(func(pro *components.Progress) func() {
			fileName := needToDownload[pro.Id-1]
			fmt.Println("Downloading:", fileName)
			url := fmt.Sprintf("https://huggingface.co/cyaoc/whisper-ggml/resolve/main/models/%s", fileName)
			resp, err := http.Get(url)
			if err != nil {
				pro.Println("get error:", err)
				return func() {}
			}
			pro.WithTotal(resp.ContentLength)
			return func() {
				defer resp.Body.Close()
				dest, err := os.OpenFile(filesToDownload[fileName], os.O_CREATE|os.O_WRONLY, 0o777)
				if err != nil {
					pro.Println("dest open error:", err)
					return
				}
				defer dest.Close()

				_, err = progress.StartTransfer(resp.Body, dest, pro)
				if err != nil {
					pro.Println("trans error:", err)
				}
			}
		}).Display()
	}
}

func setOpenVINOEnv(intelOpenVinoDir string) {
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
}

type inputs struct {
	filepath, model, language string
	useOpenVINO               bool
}

func input() (*inputs, error) {
	filepath, err := inf.NewText(
		text.WithPrompt("Enter file path or drag and drop the file."),
		text.WithPromptStyle(theme.DefaultTheme.PromptStyle),
		text.WithRequired(),
	).Display()
	if err != nil {
		return nil, err
	}
	options := []string{
		"Performance: For best quality with large model, but slower.",
		"Speed: For faster processing with medium model.",
	}
	models := []string{
		"large-v3",
		"medium",
	}
	selectKeymap := singleselect.DefaultSingleKeyMap()
	selectKeymap.Confirm = key.NewBinding(
		key.WithKeys("enter"),
		key.WithHelp("enter", "finish select"),
	)
	selectKeymap.Choice = key.NewBinding(
		key.WithKeys("enter"),
		key.WithHelp("enter", "finish select"),
	)
	selectKeymap.NextPage = key.NewBinding(
		key.WithKeys("right"),
		key.WithHelp("->", "next page"),
	)
	selectKeymap.PrevPage = key.NewBinding(
		key.WithKeys("left"),
		key.WithHelp("<-", "prev page"),
	)
	selected, err := inf.NewSingleSelect(
		options,
		singleselect.WithDisableFilter(),
		singleselect.WithKeyBinding(selectKeymap),
		singleselect.WithPageSize(2),
	).Display("Priority")
	if err != nil {
		return nil, err
	}
	language, err := inf.NewText(
		text.WithPrompt("Enter language code (e.g., 'en', 'ja', 'zh'). "),
		text.WithDefaultValue("auto"),
		text.WithPromptStyle(theme.DefaultTheme.PromptStyle),
	).Display()
	if err != nil {
		return nil, err
	}
	var useOpenVINO = false
	if isIntelCPU() {
		useOpenVINO, err = inf.NewConfirmWithSelection(
			confirm.WithPrompt("Intel CPU detected. Enable OpenVINO?"),
			confirm.WithDefaultYes(),
		).Display()
		if err != nil {
			return nil, err
		}
	}
	return &inputs{filepath, models[selected], language, useOpenVINO}, nil
}

func printOutput(r io.Reader) error {
	scanner := bufio.NewScanner(r)
	for scanner.Scan() {
		fmt.Printf("%s\n", scanner.Text())
	}
	return scanner.Err()
}

func runCommand(command string, args ...string) error {
	cmd := exec.Command(command, args...)
	cmd.Env = os.Environ()
	stdoutPipe, err := cmd.StdoutPipe()
	if err != nil {
		return fmt.Errorf("error creating stdout pipe: %w", err)
	}
	stderrPipe, err := cmd.StderrPipe()
	if err != nil {
		return fmt.Errorf("error creating stderr pipe: %w", err)
	}
	if err := cmd.Start(); err != nil {
		return fmt.Errorf("error starting command: %w", err)
	}
	errChan := make(chan error, 2)
	go func() {
		errChan <- printOutput(stdoutPipe)
	}()
	go func() {
		errChan <- printOutput(stderrPipe)
	}()
	if err := cmd.Wait(); err != nil {
		return fmt.Errorf("command finished with error: %w", err)
	}
	for i := 0; i < 2; i++ {
		if err := <-errChan; err != nil {
			return err
		}
	}
	return nil
}

func main() {
	inputs, err := input()
	if err != nil {
		fmt.Println("Error:", err)
		return
	}

	rootDir, err := GetRootDirectory(true)
	if err != nil {
		fmt.Println("Error:", err)
		return
	}
	dirs := map[string]string{
		"bin":      filepath.Join(rootDir, "bin"),
		"openvino": filepath.Join(rootDir, "bin", "openvino"),
		"models":   filepath.Join(rootDir, "models"),
		"tmp":      filepath.Join(rootDir, "tmp"),
		"outputs":  filepath.Join(rootDir, "outputs"),
	}
	if err := ensureDirsExist(dirs["models"], dirs["tmp"], dirs["outputs"]); err != nil {
		fmt.Println("Error:", err)
		return
	}
	fileName := filepath.Base(inputs.filepath)
	fileNameWithoutExtension := strings.TrimSuffix(fileName, filepath.Ext(fileName))

	ffmpegPath, err := exec.LookPath("ffmpeg")
	if err != nil {
		fmt.Println("ffmpeg not found in system path, using local version.")
		ffmpegPath = filepath.Join(rootDir, "bin", "ffmpeg")
	}
	wavFileAddress := filepath.Join(dirs["tmp"], fileNameWithoutExtension+time.Now().Format("2006-01-02_15-04-05")+".wav")
	err = runCommand(ffmpegPath, "-y", "-i", inputs.filepath, "-acodec", "pcm_s16le", "-ac", "1", "-ar", "16000", "-vn", wavFileAddress)
	if err != nil {
		fmt.Println("Error:", err)
		return
	}
	defer os.Remove(wavFileAddress)

	subType := "vtt"
	filesToDownload := map[string]string{
		fmt.Sprintf("ggml-%s.bin", inputs.model): fmt.Sprintf("%s/ggml-%s.bin", dirs["models"], inputs.model),
	}
	whisperPath := filepath.Join(dirs["bin"], "main")
	if inputs.useOpenVINO {
		filesToDownload[fmt.Sprintf("ggml-%s-encoder-openvino.xml", inputs.model)] = fmt.Sprintf("%s/ggml-%s-encoder-openvino.xml", dirs["models"], inputs.model)
		filesToDownload[fmt.Sprintf("ggml-%s-encoder-openvino.bin", inputs.model)] = fmt.Sprintf("%s/ggml-%s-encoder-openvino.bin", dirs["models"], inputs.model)
		setOpenVINOEnv(dirs["openvino"])
		whisperPath = filepath.Join(dirs["openvino"], "main")
	}
	checkAndDownload(filesToDownload)

	err = runCommand(whisperPath, "-m", filesToDownload[fmt.Sprintf("ggml-%s.bin", inputs.model)], "-l", inputs.language, "-f", wavFileAddress, fmt.Sprintf("-o%s", subType))
	if err != nil {
		fmt.Println("Error:", err)
		return
	}

	index := 0
	extension := "." + subType
	newName := fileNameWithoutExtension + extension
	for {
		_, err := os.Stat(filepath.Join(dirs["outputs"], newName))
		if os.IsNotExist(err) {
			break
		}
		index++
		newName = fmt.Sprintf("%s(%d)%s", fileNameWithoutExtension, index, extension)
	}
	outputAddress := filepath.Join(dirs["outputs"], newName)
	if err := os.Rename(wavFileAddress+extension, outputAddress); err != nil {
		fmt.Println("Failed to move and rename VTT file:", err)
		return
	}
	fmt.Println("VTT file moved and renamed to ", outputAddress)
}
