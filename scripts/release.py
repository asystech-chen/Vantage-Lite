#!/usr/bin/env python3
"""Vantage Lite 发版脚本。

用法：
    python3 scripts/release.py 0.24.1 [--dry-run] [--skip-site]

前提：
    - 代码改动已提交，工作区干净，main 与 origin/main 一致
    - CHANGELOG.md 顶部已经写好对应版本的更新日志
    - asys 官网仓库位于 ~/asys（可用 --site-dir 指定）

脚本会做：
    1. 写入 csproj 版本号
    2. dotnet build -c Release
    3. commit（版本号如有变化）/ tag vX.Y.Z / push main 与 tag
    4. 复制 exe 到官网下载目录，更新 index.html 的版本、日期、链接、SHA256
    5. 从 CHANGELOG.md 生成 changelog.html 的版本区块与侧栏条目
    6. commit + push 官网（SSH）

不做的：
    - 不创建 GitHub Release（需要 token / gh，建议在网页或 Windows 端操作）
"""

import argparse
import datetime
import hashlib
import os
import re
import shutil
import subprocess
import sys
from pathlib import Path

VERSION_RE = re.compile(r"^\d+\.\d+\.\d+$")


def run(cmd, cwd, dry_run=False):
    print("$ " + " ".join(str(c) for c in cmd))
    if dry_run:
        return ""
    result = subprocess.run(
        [str(c) for c in cmd], cwd=str(cwd), check=True,
        stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True)
    return result.stdout


def git(repo, *args, dry_run=False):
    return run(["git", *args], repo, dry_run=dry_run)


def read(path):
    return path.read_text(encoding="utf-8")


def write(path, text, dry_run=False):
    print("write " + str(path))
    if not dry_run:
        path.write_text(text, encoding="utf-8")


def fail(message):
    print("错误：" + message)
    sys.exit(1)


def resolve_dotnet():
    found = shutil.which("dotnet")
    if found:
        return found
    fallback = Path.home() / ".dotnet" / "dotnet"
    return str(fallback) if fallback.exists() else None


def changelog_section(changelog, version):
    pattern = re.compile(
        r"^# Vantage Lite " + re.escape(version) + r" 更新日志\s*$(.*?)(?=^# Vantage Lite |\Z)",
        re.MULTILINE | re.DOTALL)
    match = pattern.search(changelog)
    return match.group(1).strip() if match else None


def escape_html(text):
    return (text.replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;"))


def inline_md(text):
    text = escape_html(text)
    text = re.sub(r"`([^`]+)`", r"<code>\1</code>", text)
    return text


def section_to_html(section):
    items = []
    heading = ""
    for line in section.splitlines():
        line = line.strip()
        if line.startswith("## "):
            heading = line[3:].strip()
            continue
        if heading == "发布":
            continue
        if not line.startswith("- "):
            continue
        text = line[2:].strip()
        match = re.match(r"^\*\*(.+?)\*\*\s*[—–-]\s*(.+)$", text)
        if match:
            items.append("<li><strong>" + inline_md(match.group(1)) + "</strong>：" +
                         inline_md(match.group(2)) + "</li>")
        else:
            match = re.match(r"^\*\*(.+?)\*\*\s*[—–-]?\s*(.*)$", text)
            if match:
                body = match.group(2).strip()
                if body:
                    items.append("<li><strong>" + inline_md(match.group(1)) + "</strong>：" +
                                 inline_md(body) + "</li>")
                else:
                    items.append("<li><strong>" + inline_md(match.group(1)) + "</strong></li>")
            else:
                items.append("<li>" + inline_md(text) + "</li>")
    return "\n".join("                    " + item for item in items)


def update_site_index(index_html, version, date_str, sha256):
    text = re.sub(r"当前版本 \d+\.\d+\.\d+ · \d{4}-\d{2}-\d{2}",
                  "当前版本 " + version + " · " + date_str, index_html)
    text = re.sub(r"download/VantageLite-\d+\.\d+\.\d+-x64\.exe",
                  "download/VantageLite-" + version + "-x64.exe", text)
    text = re.sub(r'(<code id="shaValue">)[0-9a-f]{64}(</code>)',
                  r"\g<1>" + sha256 + r"\g<2>", text)
    return text


def update_site_changelog(html, version, date_str, section):
    body = section_to_html(section)
    if not body:
        fail("CHANGELOG 中的 " + version + " 没有可转换的列表项")

    # 侧栏：新版本插到最前，并取消旧版本的 active。
    sidebar_old = re.search(
        r'(<ul class="doc-cat-items">\s*\n)(\s*<li><a href="#[^"]+" data-version="[^"]+" class="active">[^<]+</a></li>)',
        html)
    if not sidebar_old:
        fail("未找到 changelog.html 侧栏的 active 条目")
    previous = sidebar_old.group(2)
    previous_version = re.search(r'data-version="([^"]+)"', previous).group(1)
    sidebar_new = (
        '                    <li><a href="#' + version + '" data-version="' + version +
        '" class="active">' + version + '</a></li>\n'
        '                    <li><a href="#' + previous_version + '" data-version="' +
        previous_version + '">' + previous_version + '</a></li>')
    html = html.replace(sidebar_old.group(0),
                        sidebar_old.group(1) + sidebar_new + "\n", 1)

    # 旧版本去掉“最新”徽标；页面脚本的无 hash 兜底版本同步更新。
    html = html.replace(
        '<h1>' + previous_version + ' <span class="cl-tag">最新</span></h1>',
        '<h1>' + previous_version + '</h1>', 1)
    html = html.replace("var fallback = '" + previous_version + "';",
                        "var fallback = '" + version + "';", 1)

    # 新版本区块插到第一个 <section class="cl-version" 之前。
    section_html = (
        '            <section class="cl-version active" data-version="' + version + '">\n'
        '                <h1>' + version + ' <span class="cl-tag">最新</span></h1>\n'
        '                <p class="cl-date">' + date_str + '</p>\n'
        '                <ul>\n' + body + '\n'
        '                </ul>\n'
        '            </section>\n\n')
    anchor = '            <section class="cl-version'
    if anchor not in html:
        fail("未找到 changelog.html 的版本区块")
    html = html.replace(anchor, section_html + anchor, 1)

    # 旧区块的 active 类去掉（只保留新版本）。
    first_section_end = html.find('</section>')
    tail = html[first_section_end:]
    tail = tail.replace('<section class="cl-version active"', '<section class="cl-version"', 1)
    html = html[:first_section_end] + tail
    return html


def main():
    parser = argparse.ArgumentParser(description="Vantage Lite 发版")
    parser.add_argument("version", help="版本号，例如 0.24.1")
    parser.add_argument("--dry-run", action="store_true", help="只打印步骤，不写入")
    parser.add_argument("--skip-site", action="store_true", help="跳过官网更新")
    parser.add_argument("--site-dir", default=str(Path.home() / "asys"), help="asys 仓库路径")
    args = parser.parse_args()

    version = args.version
    if not VERSION_RE.match(version):
        fail("版本号格式应为 X.Y.Z")

    repo = Path(__file__).resolve().parents[1]
    asys = Path(args.site_dir)
    date_str = datetime.date.today().isoformat()

    changelog = read(repo / "CHANGELOG.md")
    section = changelog_section(changelog, version)
    if not section:
        fail("CHANGELOG.md 顶部没有 " + version + " 的更新日志")

    status = git(repo, "status", "--porcelain", dry_run=args.dry_run).strip()
    if status and not args.dry_run:
        fail("工作区不干净，先提交代码改动：\n" + status)

    git(repo, "fetch", "origin", "--prune", dry_run=args.dry_run)
    ahead = git(repo, "rev-list", "--count", "origin/main..HEAD", dry_run=args.dry_run).strip()
    behind = git(repo, "rev-list", "--count", "HEAD..origin/main", dry_run=args.dry_run).strip()
    if (ahead and ahead != "0") or (behind and behind != "0"):
        fail("main 与 origin/main 不一致（ahead=" + str(ahead) + ", behind=" + str(behind) + "）")

    csproj = repo / "VantageLite.csproj"
    csproj_text = read(csproj)
    new_csproj = re.sub(r"<Version>[^<]+</Version>", "<Version>" + version + "</Version>", csproj_text, count=1)
    if new_csproj != csproj_text:
        write(csproj, new_csproj, dry_run=args.dry_run)

    dotnet = resolve_dotnet()
    if not dotnet:
        fail("找不到 dotnet")
    run([dotnet, "build", "-c", "Release"], repo, dry_run=args.dry_run)

    exe = repo / "bin" / "Release" / "net48" / "VantageLite.exe"
    if not args.dry_run:
        if not exe.exists():
            fail("构建产物不存在：" + str(exe))
        sha256 = hashlib.sha256(exe.read_bytes()).hexdigest()
    else:
        sha256 = "0" * 64
    print("exe sha256 = " + sha256)

    if new_csproj != csproj_text:
        git(repo, "add", "VantageLite.csproj", "CHANGELOG.md", dry_run=args.dry_run)
        git(repo, "commit", "-m", "chore: 发布 " + version, dry_run=args.dry_run)

    existing_tags = git(repo, "tag", "--list", "v" + version, dry_run=args.dry_run).strip()
    if existing_tags:
        fail("tag v" + version + " 已存在")
    git(repo, "tag", "-a", "v" + version, "-m", "Vantage Lite " + version, dry_run=args.dry_run)
    git(repo, "push", "origin", "main", dry_run=args.dry_run)
    git(repo, "push", "origin", "v" + version, dry_run=args.dry_run)

    if args.skip_site:
        print("已跳过官网更新。")
    else:
        if not asys.exists():
            fail("官网仓库不存在：" + str(asys))
        site_status = git(asys, "status", "--porcelain", dry_run=args.dry_run).strip()
        if site_status and not args.dry_run:
            fail("官网仓库工作区不干净，先处理：\n" + site_status)

        download = asys / "vantage" / "lite" / "download" / ("VantageLite-" + version + "-x64.exe")
        print("copy " + str(exe) + " -> " + str(download))
        if not args.dry_run:
            shutil.copy2(exe, download)

        index_path = asys / "vantage" / "lite" / "index.html"
        write(index_path, update_site_index(read(index_path), version, date_str, sha256), dry_run=args.dry_run)

        changelog_path = asys / "vantage" / "lite" / "changelog.html"
        write(changelog_path, update_site_changelog(read(changelog_path), version, date_str, section),
              dry_run=args.dry_run)

        git(asys, "add", "vantage/lite/index.html", "vantage/lite/changelog.html", str(download), dry_run=args.dry_run)
        git(asys, "commit", "-m", "feat: 更新 Vantage Lite " + version + " 下载与更新日志", dry_run=args.dry_run)
        # origin 的 HTTPS 经常不可达，统一走 SSH。
        git(asys, "push", "git@github.com:Liangchenxu/asys.git", "gh-pages", dry_run=args.dry_run)
        print("官网已推送，GitHub Pages 约 1-3 分钟生效。")

    print("")
    print("完成：" + version)
    print("还需要手动创建 GitHub Release（无 gh/token 时）：")
    print("  https://github.com/asystech-chen/Vantage-Lite/releases/new?tag=v" + version)
    print("  资产：" + str(exe))
    print("  SHA256：" + sha256)


if __name__ == "__main__":
    main()
