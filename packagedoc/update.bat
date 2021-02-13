cd dst
if exist .git goto add
git init
git checkout --orphan gh-pages
git remote add origin git@github.com:gehtsoft-usa/Gehtsoft.EF.git
:add
git add .
git commit -m "Update documentation"
git push --set-upstream origin gh-pages --force
rmdir .git /q /s