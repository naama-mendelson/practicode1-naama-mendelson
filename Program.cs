using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Linq;
using System.Linq.Expressions;

// --- OPTIONS --- //
var languageOption = new Option<string[]>(
    new string[] { "--language", "-l" },
    "Languages to include (comma separated, or 'all')")
{ IsRequired = true };

var outputOption = new Option<FileInfo>(
    new string[] { "--output", "-o" },
    "Output file path and name")
{ IsRequired = true };

var noteOption = new Option<bool>(
    new string[] { "--note", "-n" },
    "Include note with source file info");

var sortOption = new Option<string>(
    new string[] { "--sort", "-s" }, () => "name",
    "Sort files by 'name' or 'type'")
;

var removeEmptyLinesOption = new Option<bool>(
    new string[] { "--remove-empty-lines", "-r" },
    "Remove empty lines");

var authorOption = new Option<string>(
    new string[] { "--author", "-a" },
    "Author name to include at the top");

// --- BUNDLE COMMAND --- //
var bundleCommand = new Command("bundle", "Bundle code files to a single file");
bundleCommand.AddOption(languageOption);
bundleCommand.AddOption(outputOption);
bundleCommand.AddOption(noteOption);
bundleCommand.AddOption(sortOption);
bundleCommand.AddOption(removeEmptyLinesOption);
bundleCommand.AddOption(authorOption);

bundleCommand.SetHandler(
    (string[] languages, FileInfo output, bool note, string sort, bool removeEmpty, string author) =>
    {
        try
        {
            // בדיקת תיקיית מקור קיימת
            var sourceFolder = new DirectoryInfo(Directory.GetCurrentDirectory());
            if (!sourceFolder.Exists)
            {
                Console.WriteLine("Error: Source folder not found!");
                return;
            }

            // בדיקה אם sort תקין
            var validSorts = new[] { "name", "type" };
            if (!validSorts.Contains(sort.ToLower()))
            {
                Console.WriteLine("Error: Invalid sort option. Must be 'name' or 'type'.");
                return;
            }

            // קבלת כל הקבצים (מלבד bin/debug)
            var allFiles = sourceFolder.GetFiles("*.*", SearchOption.AllDirectories)
                .Where(f => !f.DirectoryName.Split(Path.DirectorySeparatorChar)
                    .Any(d => d.Equals("bin", StringComparison.OrdinalIgnoreCase) ||
                              d.Equals("debug", StringComparison.OrdinalIgnoreCase)))
                .Select(f => f.FullName)
                .ToList();

            // סינון לפי שפות
            // סינון לפי שפות
            string[] validExtensions = { ".cs", ".js", ".py", ".cpp" };
            List<string> selectedFiles;

            var lowerLangs = languages.Select(l => l.ToLower()).ToArray();

            if (lowerLangs.Length == 1 && lowerLangs[0] == "all")
            {
                // כל הסיומות התקפות
                selectedFiles = allFiles
                    .Where(f => validExtensions.Contains(Path.GetExtension(f).ToLower()))
                    .ToList();
            }
            else
            {
                // המרת שפות לסיומות
                var extensions = lowerLangs.Select(lang => lang switch
                {
                    "cs" => ".cs",
                    "js" => ".js",
                    "py" => ".py",
                    "cpp" => ".cpp",
                    _ => null
                })
                .Where(ext => !string.IsNullOrEmpty(ext))
                .ToList();

                // סינון הקבצים לפי הסיומות שבחר המשתמש
                selectedFiles = allFiles
                    .Where(f => extensions.Contains(Path.GetExtension(f).ToLower()))
                    .ToList();
            }

            if (!selectedFiles.Any())
            {
                Console.WriteLine("No code files found for the specified languages.");
                return;
            }

            // מיון
            if (sort.ToLower() == "name")
                selectedFiles.Sort();
            else if (sort.ToLower() == "type")
                selectedFiles.Sort((f1, f2) => Path.GetExtension(f1).CompareTo(Path.GetExtension(f2)));

            // כתיבה לקובץ
            using (var writer = new StreamWriter(output.FullName))
            {
                if (!string.IsNullOrWhiteSpace(author))
                    writer.WriteLine($"// Author: {author}");

                foreach (var file in selectedFiles)
                {
                    if (note)
                        writer.WriteLine($"// Source: {file}");

                    var lines = File.ReadAllLines(file);
                    if (removeEmpty)
                        lines = lines.Where(l => !string.IsNullOrWhiteSpace(l)).ToArray();

                    foreach (var line in lines)
                        writer.WriteLine(line);
                    writer.WriteLine();

                    writer.WriteLine("*****************************************************************"); // 50 כוכביות
                    writer.WriteLine();
                }
            }

            Console.WriteLine($"Bundle created: {output.FullName}");
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error: " + ex.Message);
        }
    },
    languageOption, outputOption, noteOption, sortOption, removeEmptyLinesOption, authorOption
);



//             --- CREATE-RSP COMMAND --- //
var createRspCommand = new Command("create-rsp", "Create a response file for the bundle command");
createRspCommand.SetHandler(() =>
{
    // --- בדיקה של תיקיית פלט ---
    FileInfo outputFileInfo;
    while (true)
    {
        Console.Write("Output file name (with path if needed): ");
        string outputInput = Console.ReadLine()?.Trim();
        if (string.IsNullOrWhiteSpace(outputInput))
        {
            Console.WriteLine("Error: Output file name cannot be empty. Please try again.");
            continue;
        }
        outputFileInfo = new FileInfo(outputInput);
        try
        {
            if (outputFileInfo.Directory == null || !outputFileInfo.Directory.Exists)
            {
                Console.WriteLine("Error: Directory does not exist. Please enter a valid path.");
                continue;
            }
            break;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}. Please try again.");
        }
    }

    // --- בדיקה של שפות ---
    string[] validLanguages = { "cs", "js", "py", "cpp" };
    string[] languages;
    while (true)
    {
        Console.Write("Languages (comma separated, or 'all'): ");
        string langInput = Console.ReadLine()?.Trim();
        if (string.IsNullOrWhiteSpace(langInput))
        {
            Console.WriteLine("Error: Languages cannot be empty. Please try again.");
            continue;
        }

        languages = langInput.Split(',', StringSplitOptions.RemoveEmptyEntries)
                             .Select(l => l.Trim().ToLower())
                             .ToArray();

        if (languages.Length == 0)
        {
            Console.WriteLine("Error: Invalid input. Please try again.");
            continue;
        }

        bool allValid = languages.All(l => l == "all" || validLanguages.Contains(l));
        if (!allValid)
        {
            Console.WriteLine("Error: One or more languages are invalid.");
            Console.WriteLine("Valid languages are: " + string.Join(", ", validLanguages));
            continue;
        }
        break;
    }

    // --- בדיקה של Include Note ---
    bool note = false;
    while (true)
    {
        Console.Write("Include note? (y/n): ");
        string noteInput = Console.ReadLine()?.Trim().ToLower();
        if (noteInput == "y") { note = true; break; }
        else if (noteInput == "n") { note = false; break; }
        else { Console.WriteLine("Invalid input. Please enter 'y' or 'n'."); }
    }

    // --- בדיקה של Sort ---
    string sort;
    while (true)
    {
        Console.Write("Sort by 'name' or 'type' (default: name): ");
        sort = Console.ReadLine()?.Trim().ToLower();
        if (string.IsNullOrEmpty(sort)) { sort = "name"; break; }
        if (sort == "name" || sort == "type") break;
        Console.WriteLine("Invalid input. Please enter 'name' or 'type'.");
    }

    // --- בדיקה של Remove Empty Lines ---
    bool removeEmpty = false;
    while (true)
    {
        Console.Write("Remove empty lines? (y/n): ");
        string removeInput = Console.ReadLine()?.Trim().ToLower();
        if (removeInput == "y") { removeEmpty = true; break; }
        else if (removeInput == "n") { removeEmpty = false; break; }
        else { Console.WriteLine("Invalid input. Please enter 'y' or 'n'."); }
    }

    // --- Author Name ---
    Console.Write("Author name (optional): ");
    string author = Console.ReadLine()?.Trim();

    // --- Response file name ---
    string rspFileName;
    while (true)
    {
        Console.Write("Enter response file name (.rsp): ");
        rspFileName = Console.ReadLine()?.Trim();
        if (!string.IsNullOrWhiteSpace(rspFileName)) break;
        Console.WriteLine("Error: Response file name cannot be empty.");
    }

    // --- בניית התוכן של ה-RSP ---
    var rspParts = new List<string>
    {
        "bundle",
        $"--output \"{outputFileInfo.FullName}\"",
        $"--language {string.Join(' ', languages)}"
    };

    if (note) rspParts.Add("--note");
    if (!string.IsNullOrEmpty(sort)) rspParts.Add($"--sort {sort}");
    if (removeEmpty) rspParts.Add("--remove-empty-lines");
    if (!string.IsNullOrEmpty(author)) rspParts.Add($"--author \"{author}\"");

    string rspContent = string.Join(' ', rspParts);
    File.WriteAllText(rspFileName, rspContent);

    Console.WriteLine($"Response file '{rspFileName}' created successfully!");
});


// --- ROOT COMMAND --- //
var rootCommand = new RootCommand("File Bundler CLI");
rootCommand.AddCommand(bundleCommand);
rootCommand.AddCommand(createRspCommand);

// הרצה

await rootCommand.InvokeAsync(args);

