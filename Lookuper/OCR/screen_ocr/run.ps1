try {
    $pythonVersion = python --version 2>&1
    if ($pythonVersion -match "Python (\d+\.\d+\.\d+)") {
        Write-Host "Python is installed: $($matches[1])"
    } else {
        Write-Host "Python is installed, but couldn't find version."
    }
} catch {
    Write-Host "Python isn't installed, cannot run!!!."
    exit 1
}

Write-Host "Installing dependencies specified in requirements.txt..."
pip install -r requirements.txt

$port = if ($args.Count -gt 0) { $args[0] } else { 8000 }
uvicorn main:app --reload --host 127.0.0.1 --port $port
