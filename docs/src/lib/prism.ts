import { Prism } from 'prism-react-renderer'

;(globalThis as typeof globalThis & { Prism: typeof Prism }).Prism = Prism

require('prismjs/components/prism-csharp')
require('prismjs/components/prism-bash')
require('prismjs/components/prism-powershell')

export { Prism }
