#!/bin/bash
#
# Publishes docs/ to the orphan gh-pages branch, replacing it rather than appending to it.
#
# Why an orphan branch that is force-replaced: the WebGL payload is ~21MB, and committing it to main
# on every deploy added that much to history every time — 34 builds was 634MB of a 650MB repository.
# Here the branch is one commit deep, always, so the deployed artifact never accumulates.
#
# Why plumbing rather than checkout: building the tree with hash-object/mktree/commit-tree never
# touches the working tree or the index, so this cannot disturb an in-progress edit or leave the
# repository on the wrong branch if it fails halfway.
#
# Usage:  bash Tools/publish-pages.sh
#
set -euo pipefail

cd "$(dirname "$0")/.."

if [ ! -f docs/index.html ] || [ ! -d docs/Build ]; then
  echo "docs/ has no build in it — run the WebGL build first (touch .backrooms-build-webgl)." >&2
  exit 1
fi

version=$(grep -o '0\.1\.[0-9]*' docs/index.html | head -1 || echo "unknown")
echo "publishing $version"

# Every file under docs/Build, hashed into the object database.
build_entries=""
for file in docs/Build/*; do
  blob=$(git hash-object -w "$file")
  build_entries="${build_entries}100644 blob ${blob}\t$(basename "$file")\n"
done
build_tree=$(printf "$build_entries" | git mktree)

index_blob=$(git hash-object -w docs/index.html)

# .nojekyll stops GitHub Pages running the payload through Jekyll, which would drop any file or
# folder beginning with an underscore.
nojekyll_blob=$(printf '' | git hash-object -w --stdin)

root_tree=$(printf "040000 tree %s\tBuild\n100644 blob %s\tindex.html\n100644 blob %s\t.nojekyll\n" \
  "$build_tree" "$index_blob" "$nojekyll_blob" | git mktree)

commit=$(git commit-tree "$root_tree" -m "Publish WebGL build $version

Orphan branch: this is the deployed artifact, not source. It is force-replaced on
every deploy rather than appended to, so the payload never accumulates in history.
Source lives on main.")

git update-ref refs/heads/gh-pages "$commit"
git push origin gh-pages --force

echo "pushed gh-pages $(git rev-parse --short gh-pages) ($version)"
echo "Pages will rebuild; verify at https://valectric.github.io/BackroomsDemo/"
