; ModuleID = 'arrays'
source_filename = "arrays"

define i8* @createByteArray(i64 %size) {
  %arr = alloca i8, i64 %size
  ret i8* %arr
}

define i8 @getByteArrayItem(i8* %arr, i64 %index) {
  %itemPtr = getelementptr inbounds i8, i8* %arr, i64 %index
  %item = load i8, i8* %itemPtr, align 16
  ret i8 %item
}