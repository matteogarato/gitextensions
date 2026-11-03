#!/usr/bin/env bash
set -euo pipefail

usage() {
    printf 'Usage: %s --install PUBLISH_DIRECTORY | --uninstall\n' "$0" >&2
    exit 2
}

quote_exec_argument() {
    local value=$1
    value=${value//\\/\\\\}
    value=${value//\"/\\\"}
    value=${value//\$/\\\$}
    value=${value//\`/\\\`}
    value=${value//%/%%}
    printf '"%s"' "$value"
}

data_home="${XDG_DATA_HOME:-${HOME:?HOME must be set}/.local/share}"
if [[ $data_home != /* || $data_home == *$'\n'* || $data_home == *$'\r'* ]]; then
    printf 'XDG_DATA_HOME must be an absolute path without line breaks.\n' >&2
    exit 1
fi

applications_directory="$data_home/applications"
desktop_entry="$applications_directory/gitextensions-avalonia.desktop"
icon_directory="$data_home/icons/hicolor/64x64/apps"
icon_file="$icon_directory/gitextensions-avalonia.ico"
temporary_desktop=''

cleanup() {
    if [[ -n $temporary_desktop ]]; then
        rm -f -- "$temporary_desktop"
    fi
}
trap cleanup EXIT

case "${1:-}" in
    --install)
        [[ $# -eq 2 ]] || usage

        if ! publish_directory=$(cd -- "$2" && pwd -P); then
            printf 'Publish directory does not exist: %s\n' "$2" >&2
            exit 1
        fi

        executable="$publish_directory/GitExtensions.Avalonia"
        source_icon="$publish_directory/git-extensions-logo.ico"
        if [[ ! -x $executable ]]; then
            printf 'Avalonia apphost is not executable: %s\n' "$executable" >&2
            exit 1
        fi
        if [[ ! -f $source_icon ]]; then
            printf 'Published application icon was not found: %s\n' "$source_icon" >&2
            exit 1
        fi
        if [[ $executable == *$'\n'* || $executable == *$'\r'* ]]; then
            printf 'Publish directory must not contain line breaks.\n' >&2
            exit 1
        fi

        mkdir -p -- "$applications_directory" "$icon_directory"
        cp -- "$source_icon" "$icon_file"
        quoted_executable=$(quote_exec_argument "$executable")
        temporary_desktop="$applications_directory/.gitextensions-avalonia.$$.desktop"

        cat > "$temporary_desktop" <<EOF
[Desktop Entry]
Type=Application
Name=Git Extensions (Avalonia)
Comment=Browse Git repositories
Exec=$quoted_executable browse %f
Icon=$icon_file
Terminal=false
Categories=Development;RevisionControl;
MimeType=inode/directory;
StartupNotify=true
EOF

        if command -v desktop-file-validate >/dev/null 2>&1; then
            desktop-file-validate "$temporary_desktop"
        fi
        chmod 644 "$temporary_desktop"
        mv -f -- "$temporary_desktop" "$desktop_entry"
        temporary_desktop=''
        ;;
    --uninstall)
        [[ $# -eq 1 ]] || usage
        rm -f -- "$desktop_entry" "$icon_file"
        ;;
    *)
        usage
        ;;
esac

if command -v update-desktop-database >/dev/null 2>&1 && [[ -d $applications_directory ]]; then
    update-desktop-database "$applications_directory"
fi
