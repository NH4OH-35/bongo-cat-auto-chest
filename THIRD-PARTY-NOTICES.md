# Third-party notices

BongoClaim ships Mono.Cecil 0.11.6, from [jbevain/cecil](https://github.com/jbevain/cecil), under the MIT license. It reads and writes managed metadata without executing the target assembly.

The build obtains the package from NuGet at the exact version and verifies the package's SHA-256:

```text
mono.cecil 0.11.6
D2A23832AAA948BA9A01ACC42B5726E34C5F995958F1B30D45C0E7C70B3A72D5
```

## Mono.Cecil license

Copyright (c) 2008 - 2015 Jb Evain
Copyright (c) 2008 - 2011 Novell, Inc.

Permission is hereby granted, free of charge, to any person obtaining
a copy of this software and associated documentation files (the
"Software"), to deal in the Software without restriction, including
without limitation the rights to use, copy, modify, merge, publish,
distribute, sublicense, and/or sell copies of the Software, and to
permit persons to whom the Software is furnished to do so, subject to
the following conditions:

The above copyright notice and this permission notice shall be
included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

## Project references

The archived [zelda0079/Bongo-Cat-Auto-Claim](https://github.com/zelda0079/Bongo-Cat-Auto-Claim) inspired this project's goal. Its game DLL is neither included in this source tree nor redistributed in releases. BongoClaim is a new patcher implementation, not a repackaging of that DLL.

[morluto/rea](https://github.com/morluto/rea), MIT licensed, was used for local static analysis during development. REA is not a runtime dependency or part of the downloaded package.

Bongo Cat, Unity, Steam and their assemblies belong to their respective owners. Releases contain only this tool and its stated dependency. Users supply their own installed game.
