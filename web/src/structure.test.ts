import { describe, expect, it } from 'vitest'

// Every component is a folder <Name>/ holding <Name>.tsx and <Name>.css. LegacyRedirect renders no markup, so it
// has no stylesheet.
const NO_STYLESHEET = ['LegacyRedirect']

const componentFiles = Object.keys(import.meta.glob('./{pages,components}/*/*.{tsx,css}'))
const rootTsxFiles = Object.keys(import.meta.glob('./*.tsx'))

/** "./pages/DayPage/DayPage.tsx" → "./pages/DayPage" */
function folderOf(path: string): string {
  return path.slice(0, path.lastIndexOf('/'))
}

const folders = [...new Set(componentFiles.map(folderOf))]

describe('component folders', () => {
  it('finds the pages and the components', () => {
    expect(folders.filter((folder) => folder.startsWith('./pages/'))).not.toHaveLength(0)
    expect(folders.filter((folder) => folder.startsWith('./components/'))).not.toHaveLength(0)
  })

  it.each(folders)('%s holds a .tsx and a .css named after it', (folder) => {
    const name = folder.slice(folder.lastIndexOf('/') + 1)
    expect(componentFiles).toContain(`${folder}/${name}.tsx`)
    if (!NO_STYLESHEET.includes(name)) {
      expect(componentFiles).toContain(`${folder}/${name}.css`)
    }
  })

  it('keeps only the entry points directly in src', () => {
    expect(rootTsxFiles.sort()).toEqual(['./App.tsx', './main.tsx'])
  })
})
