'use client'

import { Fragment } from 'react'
import { Highlight, themes } from 'prism-react-renderer'

import '@/lib/prism'

export function Fence({
  children,
  language,
}: {
  children: string
  language: string
}) {
  return (
    <Highlight
      code={children.trimEnd()}
      language={language}
      theme={themes.oneDark}
    >
      {({ className, style, tokens, getTokenProps }) => (
        <pre
          className={`${className} font-mono`}
          style={{ ...style, backgroundColor: 'transparent' }}
        >
          <code>
            {tokens.map((line, lineIndex) => (
              <Fragment key={lineIndex}>
                {line
                  .filter((token) => !token.empty)
                  .map((token, tokenIndex) => (
                    <span key={tokenIndex} {...getTokenProps({ token })} />
                  ))}
                {'\n'}
              </Fragment>
            ))}
          </code>
        </pre>
      )}
    </Highlight>
  )
}
