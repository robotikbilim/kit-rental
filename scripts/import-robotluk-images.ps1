param(
    [int] $DelaySeconds = 30,
    [switch] $Force
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$targetDirectory = Join-Path $PSScriptRoot '..\KitRental.Web\src\KitRental.Web.Mvc\wwwroot\images\catalog\robotluk'
New-Item -ItemType Directory -Path $targetDirectory -Force | Out-Null

$images = [ordered]@{
    'keypad-4x4.jpg'          = 'https://www.robotluk.com/idea/ed/67/myassets/products/697/keypad_min.jpg?revision=1697143329'
    'switch-red.jpg'          = 'https://www.robotluk.com/idea/ed/67/myassets/products/682/swkirmizii_min.jpg?revision=1697143329'
    'relay-5v-1ch.jpg'        = 'https://www.robotluk.com/idea/ed/67/myassets/products/661/role5v1kanall_min.jpg?revision=1697143329'
    'pot-rv09-10k.jpg'        = 'https://www.robotluk.com/idea/ed/67/myassets/products/660/pot-f_min.jpg?revision=1697143329'
    'pot-10k.jpg'             = 'https://www.robotluk.com/idea/ed/67/myassets/products/659/pot10kviadlai_min.jpg?revision=1697143329'
    'buzzer-5v.jpg'           = 'https://www.robotluk.com/idea/ed/67/myassets/products/644/st0701020_min.jpg?revision=1697143329'
    'button-dc180-blue.jpg'   = 'https://www.robotluk.com/idea/ed/67/myassets/products/738/dc180mavi_min.jpg?revision=1697143329'
    'led-10-green.jpg'        = 'https://www.robotluk.com/idea/ed/67/myassets/products/705/10mmyesil_min.jpg?revision=1697143329'
    'led-10-yellow.jpg'       = 'https://www.robotluk.com/idea/ed/67/myassets/products/704/10mmsari_min.jpg?revision=1697143329'
    'led-10-blue.jpg'         = 'https://www.robotluk.com/idea/ed/67/myassets/products/703/10mmmavi_min.jpg?revision=1697143329'
    'led-10-red.jpg'          = 'https://www.robotluk.com/idea/ed/67/myassets/products/702/10mmkirmizi_min.jpg?revision=1697143329'
    'rgb-led-10.jpg'          = 'https://www.robotluk.com/idea/ed/67/myassets/products/651/rgbled10mm-mat_min.jpg?revision=1697143329'
    'led-5-green.jpg'         = 'https://www.robotluk.com/idea/ed/67/myassets/products/650/ledgreen5mm_min.jpg?revision=1697143329'
    'led-5-red.jpg'           = 'https://www.robotluk.com/idea/ed/67/myassets/products/649/ledred5mm_min.jpg?revision=1697143329'
    'rgb-led-ky016.jpg'       = 'https://www.robotluk.com/idea/ed/67/myassets/products/646/st0707010_min.jpg?revision=1697143329'
    'black-kit.jpeg'          = 'https://www.robotluk.com/idea/ed/67/myassets/products/081/tanitim_min.jpeg?revision=1779034526'
    'blue-kit.png'            = 'https://www.robotluk.com/idea/ed/67/myassets/products/001/ilk-ekran_min.png?revision=1779034597'
    'green-kit.png'           = 'https://www.robotluk.com/idea/ed/67/myassets/products/000/ilk-ekran_min.png?revision=1779034563'
    'red-kit.png'             = 'https://www.robotluk.com/idea/ed/67/myassets/products/014/ilk-ekran_min.png?revision=1779034473'
}

$downloaded = 0
$skipped = 0
foreach ($entry in $images.GetEnumerator()) {
    $target = Join-Path $targetDirectory $entry.Key
    if ((Test-Path -LiteralPath $target) -and -not $Force) {
        $skipped++
        continue
    }

    if ($downloaded -gt 0) {
        Start-Sleep -Seconds $DelaySeconds
    }

    Invoke-WebRequest $entry.Value -OutFile $target -UseBasicParsing -Headers @{
        'User-Agent' = 'KitRentalCatalogImporter/1.0 (+https://github.com/robotikbilim/kit-rental)'
    }
    $downloaded++
    Write-Output "Downloaded $($entry.Key)"
}

Write-Output "Robotluk images complete. Downloaded=$downloaded Skipped=$skipped Target=$targetDirectory"
